using UnityEngine;

namespace Emberwake.Client
{
    // A local-only visual driven by an authoritative event ID, never a damage source.
    public sealed class CombatCue : MonoBehaviour
    {
        float remaining, duration;
        Vector3 initial;
        public void Init(float lifetime){remaining=duration=lifetime;initial=transform.localScale;}
        void Update()
        {
            remaining-=Time.deltaTime;
            if(remaining<=0){Destroy(gameObject);return;}
            transform.localScale=initial*Mathf.Clamp01(remaining/duration);
        }
    }
}
