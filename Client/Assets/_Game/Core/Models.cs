using System;
using System.Collections.Generic;
using System.Linq;

namespace Emberwake.Core
{
    [Serializable] public class ModelBundle { public string platform,url,sha256,asset; }
    [Serializable] public class CardDef
    {
        public string id, name, role, description, behavior, modelKey, artUrl, bundleUrl, bundleSha256, bundleAsset;
        public int cost, count = 1, artIndex, unlockTrophies;
        public float hp, damage, interval, range, minRange, speed, splash;
        public bool enabled = true;
        public ModelBundle[] modelBundles;
    }
    [Serializable] public class OfferDef { public string id, name, description, cosmetic; public int goldCost; }
    [Serializable] public class Catalog
    {
        public int schemaVersion = 1, version = 1, minClientVersion = 1;
        public string season = "The Ember Shoals", news = "Welcome aboard. Your fleet awaits.", cardAtlasUrl;
        public CardDef[] cards;
        public OfferDef[] offers;
        public CardDef Get(string id) { return cards == null ? null : Array.Find(cards, c => c.id == id); }
        public static string Validate(Catalog c)
        {
            if (c == null || c.schemaVersion != 1 || c.version < 1 || c.minClientVersion < 1) return "Unsupported catalog version.";
            if (c.cards == null || c.cards.Length < 8 || c.cards.Length > 100) return "Catalog requires 8–100 cards.";
            var ids = new HashSet<string>();
            var behaviors = new[] { "swarm", "cutter", "tank", "mortar", "ram", "repair", "grapeshot", "smoke", "interceptor", "bomb", "battery", "tailwind" };
            foreach (var a in c.cards)
            {
                if (a == null || string.IsNullOrWhiteSpace(a.id) || a.id.Length > 48 || a.id.Any(x => !char.IsLetterOrDigit(x) && x != '_') || !ids.Add(a.id)) return "Invalid/duplicate card ID.";
                if (string.IsNullOrWhiteSpace(a.name) || a.name.Length > 40 || (a.description ?? "").Length > 400 || !behaviors.Contains(a.behavior)) return "Invalid card text or unsupported behavior.";
                if (a.cost < 1 || a.cost > 10 || a.count < 1 || a.count > 5 || a.unlockTrophies < 0 || a.artIndex < 0 || a.artIndex > 11) return "Invalid card configuration.";
                foreach (var n in new[] { a.hp, a.damage, a.interval, a.range, a.minRange, a.speed, a.splash }) if (float.IsNaN(n) || float.IsInfinity(n) || n < 0 || n > 10000) return "Invalid numeric stats.";
                bool order = a.behavior == "grapeshot" || a.behavior == "smoke" || a.behavior == "tailwind";
                if (!order && (a.hp < 1 || a.interval < .1f || a.interval > 10 || a.speed > 6 || a.range > 12 || a.minRange > a.range)) return "Invalid combat stats.";
                foreach (var url in new[] { a.artUrl, a.bundleUrl }) if (!string.IsNullOrEmpty(url) && (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != "https")) return "Remote asset URLs require HTTPS.";
                if (!string.IsNullOrEmpty(a.bundleUrl) && (string.IsNullOrEmpty(a.bundleSha256) || a.bundleSha256.Length != 64 || a.bundleSha256.Any(x => !Uri.IsHexDigit(x)) || string.IsNullOrWhiteSpace(a.bundleAsset))) return "Bundle requires SHA-256 and prefab name.";
                if(a.modelBundles!=null){var platforms=new HashSet<string>();foreach(var b in a.modelBundles)if(b==null||!new[]{"Android","Windows","iOS"}.Contains(b.platform)||!platforms.Add(b.platform)||!Uri.TryCreate(b.url,UriKind.Absolute,out var u)||u.Scheme!="https"||string.IsNullOrEmpty(b.sha256)||b.sha256.Length!=64||b.sha256.Any(x=>!Uri.IsHexDigit(x))||string.IsNullOrWhiteSpace(b.asset))return "Invalid platform model bundle.";}
            }
            if (c.cards.Count(x => x.enabled && x.unlockTrophies == 0) < 8) return "At least eight starter cards must remain enabled.";
            if (c.offers == null || c.offers.Length > 40) return "Invalid offers.";
            ids.Clear();
            foreach (var o in c.offers) if (o == null || string.IsNullOrWhiteSpace(o.id) || !ids.Add(o.id) || o.goldCost < 1 || o.goldCost > 100000 || string.IsNullOrWhiteSpace(o.cosmetic)) return "Invalid offer.";
            return null;
        }
        public static Catalog Default()
        {
            var c = new Catalog { cards = new[] {
                Make("skiffs", "Skiff Pack", "SWARM", "Three swift boats. Overwhelm lone defenders; avoid splash fire.", "swarm", 2, 95, 18, .9f, 2.5f, 2.4f, 0, 3),
                Make("cutter", "Cutter", "ESCORT", "A reliable three-cannon escort. Protect your heavy ships.", "cutter", 3, 520, 72, 1.8f, 4, 1.6f, 1),
                Make("ironclad", "Ironclad", "VANGUARD", "Armored structure hunter. Pair it with an escort.", "tank", 5, 1300, 60, 1.8f, 2.5f, .9f, 2),
                Make("mortar", "Mortar Boat", "ARTILLERY", "Long-range splash fire. Vulnerable at close range.", "mortar", 4, 360, 100, 2.4f, 6.5f, 1.1f, 3),
                Make("ram", "Ram Boat", "BREACHER", "Charges harbor defenses with a reinforced bronze bow.", "ram", 4, 540, 80, 1.4f, 1.4f, 2.5f, 4),
                Make("repair", "Repair Launch", "SUPPORT", "Heals one injured combat ship. Cannot repair structures.", "repair", 3, 320, 35, 1.2f, 3, 1.3f, 5),
                Make("grapeshot", "Grapeshot", "ORDER", "A burst that clears small boats. Reduced damage to defenses.", "grapeshot", 2, 0, 100, 0, 2.3f, 0, 6),
                Make("smoke", "Smoke Screen", "ORDER", "A four-second cloud slows enemy cannon reloads.", "smoke", 3, 0, 0, 0, 3, 0, 7),
                Make("interceptor", "Interceptor", "PURSUIT", "Fast twin-cannon pursuit boat. Hunt enemy artillery.", "interceptor", 3, 350, 48, 1, 3, 2.6f, 8),
                Make("bomb", "Bomb Ketch", "AREA DAMAGE", "Short-range explosive volleys punish clustered fleets.", "bomb", 4, 600, 85, 1.8f, 3.2f, 1.2f, 9),
                Make("battery", "Anchor Battery", "DEFENSE", "A stationary cannon platform. Lasts for 25 seconds.", "battery", 3, 650, 55, 1.2f, 5.5f, 0, 10),
                Make("tailwind", "Tailwind", "ORDER", "Four seconds of faster movement for ships in the zone.", "tailwind", 2, 0, 0, 0, 3.5f, 0, 11)
            }, offers = new[] {
                new OfferDef { id="copper_sails", name="Copper Horizon", description="Warm copper trim for your fleet.", cosmetic="copper", goldCost=450 },
                new OfferDef { id="night_sails", name="Midnight Fleet", description="Deep indigo sails and silver accents.", cosmetic="midnight", goldCost=700 },
                new OfferDef { id="coral_sails", name="Coral Captain", description="Sunset coral sails for your harbor.", cosmetic="coral", goldCost=950 }
            }};
            c.Get("mortar").minRange = 2.5f; c.Get("mortar").splash = 1.1f; c.Get("bomb").splash = 1.6f;
            for (int i=8;i<12;i++) c.cards[i].unlockTrophies = (i-7)*60;
            return c;
        }
        static CardDef Make(string id,string name,string role,string desc,string behavior,int cost,float hp,float damage,float interval,float range,float speed,int art,int count=1)
        { return new CardDef { id=id,name=name,role=role,description=desc,behavior=behavior,cost=cost,hp=hp,damage=damage,interval=interval,range=range,speed=speed,artIndex=art,count=count,modelKey=id }; }
    }
    [Serializable] public class CardProgress { public string id; public int level = 1, blueprints; }
    [Serializable] public class MatchRecord { public string id, opponent, result, date; public int gold, trophies; }
    [Serializable] public class Profile
    {
        public string id, name = "Captain", equipped = "default";
        public int gold = 300, trophies, seasonXp, wins, battles;
        public bool tutorialDone;
        public string[] deck = { "skiffs", "cutter", "ironclad", "mortar", "ram", "repair", "grapeshot", "smoke" };
        public List<CardProgress> cards = new List<CardProgress>();
        public List<string> cosmetics = new List<string> { "default" };
        public List<int> claimedSeason = new List<int>();
        public List<MatchRecord> history = new List<MatchRecord>();
        public CardProgress Card(string key) { return cards.Find(c=>c.id==key); }
        public void Unlock(Catalog catalog) { foreach(var c in catalog.cards) if(c.enabled && trophies>=c.unlockTrophies && Card(c.id)==null) cards.Add(new CardProgress{id=c.id}); }
    }
    [Serializable] public class AuthRequest { public string credential, name; }
    [Serializable] public class AuthResponse { public string token, credential; public Profile profile; }
    [Serializable] public class DeckRequest { public string[] deck; }
    [Serializable] public class IdRequest { public string id; public int tier; }
    [Serializable] public class ErrorResponse { public string error; }
    [Serializable] public class QueueReply { public string status, matchId, ticket, opponent, transport, error; public int team; public Catalog catalog; }
    [Serializable] public class DeployRequest { public long sequence; public int slot; public float x,z; public string expectedCard; }
    [Serializable] public class CommandReply { public long sequence; public bool accepted; public string error; }
    [Serializable] public class Bootstrap { public int minClientVersion=1; public string catalogUrl="/v1/catalog", photonAppId="", privacyUrl="", termsUrl="", battleTransport="fusion"; public bool onlineEnabled=true, purchasesEnabled=false; }
}
