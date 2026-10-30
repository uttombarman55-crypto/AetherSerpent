using UnityEngine;
using AetherSerpent.CameraSystem;
using AetherSerpent.Serpent;

namespace AetherSerpent.Environment
{
    /// <summary>
    /// Interactive & Reactive Environment Prop (Monolith, Spire, Crag Rock).
    /// Reacts physically to the serpent:
    /// - Heavy serpent contact triggers tremors and ground dust
    /// - High-velocity boost or Titan Seismic Slam fractures and topples the pillar
    /// </summary>
    public class ReactiveEnvironment : MonoBehaviour
    {
        [Header("Fracture State")]
        [SerializeField] private bool isFractured = false;
        [SerializeField] private float requiredImpactMass = 2.0f;

        private Rigidbody propRigidbody;

        private void Start()
        {
            propRigidbody = GetComponent<Rigidbody>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (isFractured) return;

            var controller = collision.gameObject.GetComponentInParent<SerpentController>();
            if (controller != null && controller.IsBoosting)
            {
                TriggerFracture(collision.contacts[0].point);
            }
        }

        public void TriggerFracture(Vector3 impactOrigin)
        {
            if (isFractured) return;
            isFractured = true;

            // Camera trauma
            if (CameraShakeManager.Instance != null)
            {
                CameraShakeManager.Instance.AddTrauma(0.45f);
            }

            // Enable dynamic physics to topple or tumble
            if (propRigidbody == null)
            {
                propRigidbody = gameObject.AddComponent<Rigidbody>();
            }

            propRigidbody.isKinematic = false;
            propRigidbody.mass = 150f;

            Vector3 blastDir = (transform.position - impactOrigin).normalized + (Vector3.up * 0.4f);
            propRigidbody.AddForce(blastDir * 1800f, ForceMode.Impulse);
            propRigidbody.AddTorque(Random.insideUnitSphere * 400f, ForceMode.Impulse);

            // Destroy or settle after duration
            Destroy(gameObject, 12f);
        }
    }
}
