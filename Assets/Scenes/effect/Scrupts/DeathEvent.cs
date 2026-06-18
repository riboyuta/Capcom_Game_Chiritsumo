using UnityEngine;

public class DeathEvent : MonoBehaviour
{
    public ParticleSystem ps;

    public void PlayParticle()
    {
        Debug.Log("ŒÄ‚Î‚ê‚½"); // Šm”F—p
        ps.Play();
    }
}