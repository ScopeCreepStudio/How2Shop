using System;
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

            ChaosManager manager = ChaosManager.Instance;
            bool reported = manager != null && manager.ReportEvent(eventType, gameObject, instigator, impactSpeed);
            if (reported && disableAfterSuccessfulImpact)
                BreakTarget();
            return reported;
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
