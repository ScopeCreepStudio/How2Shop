using System;
using How2Shop.Items;
using UnityEngine;

namespace How2Shop.Chaos
{
    [Flags]
    public enum ChaosImpactSource
    {
        None = 0,
        ThrownItem = 1,
        PlayerTrolley = 2,
        PlayerBody = 4
    }

    /// <summary>
    /// Marks a collider or parent object as a chaos-reporting target. Add this to
    /// breakable products, shelves, security guards, or checkout equipment.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChaosImpactTarget : MonoBehaviour
    {
        public const float DefaultFragileBreakImpactSpeed = 16.5f;

        [SerializeField] private ChaosEventType eventType = ChaosEventType.BreakProduct;
        [SerializeField] private ChaosImpactSource acceptedSources = ChaosImpactSource.ThrownItem;
        [SerializeField, Min(0f)] private float minimumImpactSpeed = 2.5f;
        [SerializeField] private bool disableAfterSuccessfulImpact;

        public ChaosEventType EventType => eventType;

        public void Configure(ChaosEventType newEventType, ChaosImpactSource newAcceptedSources,
            float newMinimumImpactSpeed, bool breakOnImpact)
        {
            eventType = newEventType;
            acceptedSources = newAcceptedSources;
            minimumImpactSpeed = Mathf.Max(0f, newMinimumImpactSpeed);
            disableAfterSuccessfulImpact = breakOnImpact;
        }

        public bool TryReportImpact(ChaosImpactSource source, GameObject instigator, float impactSpeed)
        {
            if ((acceptedSources & source) == 0 || impactSpeed < minimumImpactSpeed)
                return false;

            if (eventType == ChaosEventType.BreakProduct && !IsFragileProduct())
                return false;

            ChaosManager manager = ChaosManager.Instance;
            bool reported = manager != null && manager.ReportEvent(eventType, gameObject, instigator, impactSpeed);
            bool shouldBreak = reported || manager == null && eventType == ChaosEventType.BreakProduct && IsFragileProduct();
            if (disableAfterSuccessfulImpact && shouldBreak)
            {
                Vector3 breakPosition = GetProductCenter();
                if (eventType == ChaosEventType.BreakProduct)
                {
                    SpawnShatterBurst(breakPosition);
                    if (GameplaySfxPlayer.Instance != null)
                        GameplaySfxPlayer.Instance.PlayProductBreak(breakPosition, impactSpeed);
                }
                BreakTarget();
            }
            return reported;
        }

        private bool IsFragileProduct()
        {
            ItemSpriteView product = GetComponent<ItemSpriteView>();
            if (product == null)
                product = GetComponentInChildren<ItemSpriteView>(true);
            if (product == null)
                product = GetComponentInParent<ItemSpriteView>();
            return product != null && product.Item != null && product.Item.HasTag("fragile");
        }

        private Vector3 GetProductCenter()
        {
            SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
            return sprite != null ? sprite.bounds.center : transform.position;
        }

        private static void SpawnShatterBurst(Vector3 position)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return;

            var particleObject = new GameObject("Fragile Product Shatter");
            particleObject.transform.position = position;
            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.duration = 0.75f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 5.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.085f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color32(255, 247, 215, 255), new Color32(223, 202, 157, 255));
            main.gravityModifier = 1.4f;
            main.maxParticles = 32;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.09f;

            Material material = new Material(shader) { name = "Temporary Grocery Shards" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.material = material;

            particles.Play();
            Destroy(particleObject, 2f);
            Destroy(material, 2f);
        }

        private void BreakTarget()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = false;

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
        }
    }
}
