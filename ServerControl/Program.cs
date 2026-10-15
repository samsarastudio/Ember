using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Text.Json;

namespace Emberwake.ServerControl;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        string root=args.FirstOrDefault(a=>!a.StartsWith("--"))??Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../.."));
        using var mutex=new Mutex(true,"Local\\EmberwakeServerControl",out bool first);
        if(!first){MessageBox.Show("The server panel is already running. Open it from the system tray.","Emberwake");return;}
        Application.Run(new ServerPanel(root,args.Contains("--smoke-test")));
    }
}

sealed record MatchItem(string Directory,string Id,string A,string B,bool AI)
{
    public override string ToString()=>$"{A} vs {B}{(AI?" · AI":"")} — {Id[..8]}";
}

sealed class ServerPanel : Form
{
    readonly string root,package,matchRoot,logs;
    readonly NotifyIcon tray=new();
    readonly Label status=new(){AutoSize=true,Text="Checking server…",Padding=new Padding(12)};
    readonly Label detail=new(){AutoSize=true,Text="Select an active match to watch. No player slot is used.",Padding=new Padding(12)};
    readonly ComboBox matches=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Top,Height=32,ForeColor=Color.Black,BackColor=Color.White};
    readonly BattleCanvas canvas=new(){Dock=DockStyle.Fill};
    readonly System.Windows.Forms.Timer timer=new(){Interval=200};
    readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(3)};
    readonly List<Button> actionButtons=new();
    bool busy,refreshing,exit;
    int counter;
    readonly bool smoke;
    public ServerPanel(string root,bool smoke)
    {
        this.root=Path.GetFullPath(root);this.smoke=smoke;
        package=Path.Combine(root,"Builds/Fusion-Windows-Worker-20261014");
        matchRoot=Path.Combine(package,"state/matches");logs=Path.Combine(package,"test-logs/control");
        System.IO.Directory.CreateDirectory(logs);
        Text="Emberwake · Server Control";Size=new Size(1050,820);MinimumSize=new Size(800,650);
        BackColor=Color.FromArgb(19,29,41);ForeColor=Color.WhiteSmoke;Font=new Font("Segoe UI",10);
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=5,ColumnCount=1,Padding=new Padding(18)};
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,45));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
        AddButton(buttons,"Start server",()=>Operate("start"));AddButton(buttons,"Restart server",()=>Operate("restart"));
        AddButton(buttons,"Stop server",()=>Operate("stop"));AddButton(buttons,"Open logs",()=>OpenFolder(Path.Combine(package,"test-logs")));
        AddButton(buttons,"Minimize to tray",()=>Hide());
        layout.Controls.Add(status,0,0);layout.Controls.Add(buttons,0,1);layout.Controls.Add(matches,0,2);layout.Controls.Add(detail,0,3);layout.Controls.Add(canvas,0,4);Controls.Add(layout);
        var menu=new ContextMenuStrip();
        menu.Items.Add("Open / Watch active games",null,(_,_)=>Restore());
        menu.Items.Add("Start server",null,async(_,_)=>await Operate("start"));
        menu.Items.Add("Restart server",null,async(_,_)=>await Operate("restart"));
        menu.Items.Add("Stop server",null,async(_,_)=>await Operate("stop"));
        menu.Items.Add("Open logs",null,(_,_)=>OpenFolder(Path.Combine(package,"test-logs")));menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit panel (keep server running)",null,(_,_)=>{exit=true;Close();});
        tray.Icon=SystemIcons.Application;tray.Text="Emberwake server";tray.ContextMenuStrip=menu;tray.Visible=true;tray.DoubleClick+=(_,_)=>Restore();
        Resize+=(_,_)=>{if(WindowState==FormWindowState.Minimized)Hide();};
        FormClosing+=(_,e)=>{if(!exit){e.Cancel=true;Hide();}};
        FormClosed+=(_,_)=>{timer.Stop();tray.Dispose();http.Dispose();};
        timer.Tick+=async(_,_)=>{ReadObserver();if(++counter%10==1)await RefreshStatus();};
        Shown+=async(_,_)=>
        {
            timer.Start();await RefreshStatus();
            if(smoke){await Task.Delay(12000);using var image=new Bitmap(Width,Height);DrawToBitmap(image,new Rectangle(Point.Empty,Size));image.Save(Path.Combine(logs,"panel-smoke.png"));File.WriteAllText(Path.Combine(logs,"panel-smoke.txt"),status.Text+"\n"+detail.Text+"\nMatches="+matches.Items.Count);exit=true;Close();}
        };
    }
    void AddButton(Control parent,string title,Action action)
    {
        var button=new Button{Text=title,AutoSize=true,Height=36,FlatStyle=FlatStyle.Flat,Margin=new Padding(3,0,8,0),BackColor=Color.FromArgb(37,62,79)};
        button.Click+=(_,_)=>action();parent.Controls.Add(button);actionButtons.Add(button);
    }
    void AddButton(Control parent,string title,Func<Task> action)=>AddButton(parent,title,(Action)(async()=>await action()));
    void Restore(){Show();WindowState=FormWindowState.Normal;Activate();}
    void OpenFolder(string path){System.IO.Directory.CreateDirectory(path);Process.Start(new ProcessStartInfo("explorer.exe",path){UseShellExecute=true});}
    async Task RefreshStatus()
    {
        if(refreshing)return;refreshing=true;
        try
        {
            using var response=await http.GetAsync("http://127.0.0.1:5080/health");
            response.EnsureSuccessStatusCode();using var health=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            bool ready=health.RootElement.GetProperty("rankedAvailable").GetBoolean();
            status.Text=$"SERVER {(ready?"ONLINE":"NOT READY")}  ·  royale.inmomentservices.com  ·  Fusion / US East";
            status.ForeColor=ready?Color.LightGreen:Color.Khaki;tray.Text=ready?"Emberwake · Server online":"Emberwake · Server not ready";
        }
        catch{status.Text="SERVER OFFLINE  ·  Start server to enable matchmaking and battles";status.ForeColor=Color.Salmon;tray.Text="Emberwake · Server offline";}
        finally{refreshing=false;}
        RefreshMatches();
    }
    void RefreshMatches()
    {
        var found=new List<MatchItem>();
        if(System.IO.Directory.Exists(matchRoot))foreach(string dir in System.IO.Directory.GetDirectories(matchRoot))
        {
            // Live processes only: aborted and completed rooms do not remain as stale active games.
            if(File.Exists(Path.Combine(dir,"result.json")))continue;
            string observer=Path.Combine(dir,"observer.json");
            if(!File.Exists(observer)||DateTime.UtcNow-File.GetLastWriteTimeUtc(observer)>TimeSpan.FromSeconds(10))continue;
            try{using var a=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"assignment.json")));var j=a.RootElement;
                found.Add(new MatchItem(dir,j.GetProperty("matchId").GetString()!,j.GetProperty("names")[0].GetString()!,j.GetProperty("names")[1].GetString()!,j.TryGetProperty("aiOpponent",out var ai)&&ai.GetBoolean()));}
            catch(IOException){}catch(JsonException){}
        }
        string? selected=(matches.SelectedItem as MatchItem)?.Id;
        // Keep the selected finished game's final state available until another match is chosen.
        if(matches.SelectedItem is MatchItem old&&!found.Any(m=>m.Id==old.Id)&&File.Exists(Path.Combine(old.Directory,"result.json")))found.Add(old);
        if(found.Select(m=>m.Id).SequenceEqual(matches.Items.Cast<MatchItem>().Select(m=>m.Id)))return;
        matches.BeginUpdate();matches.Items.Clear();matches.Items.AddRange(found.Cast<object>().ToArray());
        if(found.Count>0)matches.SelectedIndex=Math.Max(0,found.FindIndex(m=>m.Id==selected));
        matches.EndUpdate();if(found.Count==0){canvas.Snapshot=null;canvas.Invalidate();detail.Text="No active games. Waiting for captains to match.";}
    }
    void ReadObserver()
    {
        if(matches.SelectedItem is not MatchItem match)return;
        try
        {
            using var snapshot=JsonDocument.Parse(File.ReadAllText(Path.Combine(match.Directory,"observer.json")));
            canvas.Snapshot=snapshot.RootElement.Clone();canvas.Invalidate();
            var s=snapshot.RootElement;int winner=s.GetProperty("winner").GetInt32();
            string result=winner==-2?"LIVE":winner==-1?"DRAW":$"WINNER: {(winner==0?match.A:match.B)}";
            detail.Text=$"{result}  ·  {s.GetProperty("time").GetSingle():0}s  ·  Score {s.GetProperty("score")[0]} : {s.GetProperty("score")[1]}  ·  Blue: {match.A} / Coral: {match.B}";
        }
        catch(IOException){}catch(JsonException){}
    }
    async Task Operate(string action)
    {
        if(busy)return;
        if(action!="start"&&matches.Items.Cast<MatchItem>().Any(m=>!File.Exists(Path.Combine(m.Directory,"result.json"))))
            if(MessageBox.Show("This stops active battles. Continue?","Server "+action,MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        busy=true;actionButtons.ForEach(b=>b.Enabled=false);
        try
        {
            status.Text=$"Server {action} in progress…";
            string script=Path.Combine(root,"Tools/Control-Server.ps1");
            var start=new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(string arg in new[]{"-NoProfile","-ExecutionPolicy","Bypass","-File",script,"-Action",action})start.ArgumentList.Add(arg);
            using var process=Process.Start(start)!;
            var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();string text=await output+"\n"+await error;File.AppendAllText(Path.Combine(logs,"actions.log"),DateTime.Now+" "+action+"\n"+text+"\n");
            if(process.ExitCode!=0)throw new InvalidOperationException(text);
        }
        catch(Exception e){MessageBox.Show(e.Message,"Server action failed",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        finally{busy=false;actionButtons.ForEach(b=>b.Enabled=true);await RefreshStatus();}
    }
}

sealed class BattleCanvas : Control
{
    public JsonElement? Snapshot;
    public BattleCanvas(){DoubleBuffered=true;BackColor=Color.FromArgb(13,37,53);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        float scale=Math.Min((Width-70)/24f,(Height-30)/36f),cx=Width/2f,cy=Height/2f;
        PointF Map(float x,float z)=>new(cx+x*scale,cy-z*scale);
        using var grid=new Pen(Color.FromArgb(36,66,80));using var divider=new Pen(Color.FromArgb(75,122,135),2);
        for(int x=-12;x<=12;x+=2)g.DrawLine(grid,Map(x,-18),Map(x,18));
        for(int z=-18;z<=18;z+=2)g.DrawLine(grid,Map(-12,z),Map(12,z));
        g.DrawLine(divider,Map(-12,0),Map(12,0));
        if(Snapshot is not JsonElement s){g.DrawString("Live battle view · waiting for a match",Font,Brushes.LightGray,20,20);return;}
        foreach(var u in s.GetProperty("units").EnumerateArray())
        {
            float hp=u.GetProperty("hp").GetSingle();if(hp<=0)continue;
            var point=Map(u.GetProperty("x").GetSingle(),u.GetProperty("z").GetSingle());
            bool structure=u.GetProperty("structure").GetBoolean();float r=structure?9:5;
            using var brush=new SolidBrush(u.GetProperty("team").GetInt32()==0?Color.DeepSkyBlue:Color.Coral);
            if(structure)g.FillRectangle(brush,point.X-r,point.Y-r,r*2,r*2);else g.FillEllipse(brush,point.X-r,point.Y-r,r*2,r*2);
            g.FillRectangle(Brushes.DarkSlateGray,point.X-11,point.Y-r-6,22,3);
            g.FillRectangle(Brushes.LightGreen,point.X-11,point.Y-r-6,22*Math.Clamp(hp/u.GetProperty("maxHp").GetSingle(),0,1),3);
            if(structure)g.DrawString(u.GetProperty("hq").GetBoolean()?"HQ":"OUTPOST",Font,Brushes.WhiteSmoke,point.X+12,point.Y-8);
        }
        foreach(var shot in s.GetProperty("shots").EnumerateArray())
        {
            var p=Map(shot.GetProperty("x").GetSingle(),shot.GetProperty("z").GetSingle());g.FillEllipse(Brushes.Gold,p.X-3,p.Y-3,6,6);
        }
        foreach(var zone in s.GetProperty("zones").EnumerateArray())
        {
            var p=Map(zone.GetProperty("x").GetSingle(),zone.GetProperty("z").GetSingle());float r=zone.GetProperty("radius").GetSingle()*scale;
            using var pen=new Pen(Color.FromArgb(160,190,190,220),2);g.DrawEllipse(pen,p.X-r,p.Y-r,r*2,r*2);
        }
        g.DrawString("BLUE TEAM  ·  CORAL TEAM  ·  GOLD PROJECTILES",Font,Brushes.LightGray,12,Height-25);
    }
}
