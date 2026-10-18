using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Gives a shopper a temporary physics ragdoll when the player bumps into them,
/// then returns the shopper and their cart to the nearest walkable NavMesh point.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ShopperNPC))]
public sealed class ShopperRagdoll : MonoBehaviour
{
    [Header("Bump response")]
    [SerializeField, Min(0f)] private float minimumImpactSpeed = 0.25f;
    [SerializeField, Min(0f)] private float shopperPushImpulse = 10f;
    [SerializeField, Min(0f)] private float shopperUpwardImpulse = 7f;
    [SerializeField, Min(0f)] private float cartPushImpulse = 42f;
    [SerializeField, Min(0f)] private float cartUpwardImpulse = 12f;
    [SerializeField, Min(0.5f)] private float recoveryDelay = 2.5f;
    [SerializeField, Min(0.5f)] private float recoverySearchRadius = 5f;

    private sealed class Part
    {
        public HumanBodyBones bone;
        public Transform transform;
        public Rigidbody body;
        public CharacterJoint joint;
        public readonly List<Collider> colliders = new List<Collider>();
        public Vector3 localPosition;
        public Quaternion localRotation;
    }

    private sealed class BehaviourState
    {
        public Behaviour behaviour;
        public bool wasEnabled;
    }

    private readonly Dictionary<HumanBodyBones, Part> parts = new Dictionary<HumanBodyBones, Part>();
    private readonly List<Part> orderedParts = new List<Part>();
    private readonly List<BehaviourState> cartBehaviours = new List<BehaviourState>();

    private ShopperNPC shopper;
    private Animator animator;
    private NavMeshAgent agent;
    private CapsuleCollider bodyCollider;
    private CapsuleCollider bumpTrigger;
    private Rigidbody rootBody;
    private Transform cart;
    private Transform cartOriginalParent;
    private Rigidbody cartBody;
    private ShopperCartImpactRelay cartRelay;
    private bool cartWasChild;
    private bool cartBodyWasKinematic;
    private bool cartBodyUsedGravity;
    private bool bodyColliderWasEnabled;
    private bool rootBodyWasDetectingCollisions;
    private bool bumpTriggerWasEnabled;
    private RigidbodyConstraints cartOriginalConstraints;
    private Vector3 cartOriginalLocalPosition;
    private Quaternion cartOriginalLocalRotation;
    private Vector3 cartOriginalLocalScale;
    private Quaternion standingRotation;
    private bool ragdollActive;

    private static readonly HumanBodyBones[] PartBones =
    {
        HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
        HumanBodyBones.Neck, HumanBodyBones.Head,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes
    };

    private void Awake()
    {
        shopper = GetComponent<ShopperNPC>();
        animator = shopper.CharacterAnimator != null ? shopper.CharacterAnimator : GetComponent<Animator>();
        agent = shopper.Agent != null ? shopper.Agent : GetComponent<NavMeshAgent>();

        bodyCollider = GetComponent<CapsuleCollider>();
        if (bodyCollider == null)
        {
            bodyCollider = gameObject.AddComponent<CapsuleCollider>();
            bodyCollider.height = agent != null ? agent.height : 1.8f;
            bodyCollider.radius = agent != null ? agent.radius : 0.3f;
            bodyCollider.center = Vector3.up * (bodyCollider.height * 0.5f);
        }
        bodyColliderWasEnabled = bodyCollider.enabled;

        bumpTrigger = gameObject.AddComponent<CapsuleCollider>();
        bumpTrigger.height = bodyCollider.height + 0.1f;
        bumpTrigger.radius = bodyCollider.radius + 0.1f;
        bumpTrigger.center = bodyCollider.center;
        bumpTrigger.isTrigger = true;
        bumpTriggerWasEnabled = bumpTrigger.enabled;

        rootBody = GetComponent<Rigidbody>();
        if (rootBody == null)
            rootBody = gameObject.AddComponent<Rigidbody>();
        rootBodyWasDetectingCollisions = rootBody.detectCollisions;
        rootBody.isKinematic = true;
        rootBody.useGravity = false;
        rootBody.detectCollisions = true;
        rootBody.interpolation = RigidbodyInterpolation.Interpolate;

        CaptureCart();
        BuildRagdoll();
    }

    private void CaptureCart()
    {
        cart = shopper.ShoppingCart;
        if (cart == null)
            return;

        cartOriginalParent = cart.parent;
        cartWasChild = cart.IsChildOf(transform);
        cartOriginalLocalPosition = cartWasChild ? cart.localPosition : transform.InverseTransformPoint(cart.position);
        cartOriginalLocalRotation = cartWasChild
            ? cart.localRotation
            : Quaternion.Inverse(transform.rotation) * cart.rotation;
        cartOriginalLocalScale = cart.localScale;

        cartBody = cart.GetComponent<Rigidbody>();
        if (cartBody == null)
            cartBody = cart.gameObject.AddComponent<Rigidbody>();

        cartBodyWasKinematic = cartBody.isKinematic;
        cartBodyUsedGravity = cartBody.useGravity;
        cartOriginalConstraints = cartBody.constraints;

        Collider[] cartColliders = cart.GetComponentsInChildren<Collider>(true);
        if (cartColliders.Length == 0)
        {
            Renderer[] renderers = cart.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                BoxCollider box = cart.gameObject.AddComponent<BoxCollider>();
                box.center = cart.InverseTransformPoint(bounds.center);
                Vector3 localSize = cart.InverseTransformVector(bounds.size);
                box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
            }
        }

        BoxCollider cartBodyCollider = cart.GetComponent<BoxCollider>();
        if (cartBodyCollider != null && !cartBodyCollider.isTrigger)
        {
            BoxCollider cartBumpTrigger = cart.gameObject.AddComponent<BoxCollider>();
            cartBumpTrigger.center = cartBodyCollider.center;
            cartBumpTrigger.size = cartBodyCollider.size;
            cartBumpTrigger.isTrigger = true;
        }

        cartRelay = cart.GetComponent<ShopperCartImpactRelay>();
        if (cartRelay == null)
            cartRelay = cart.gameObject.AddComponent<ShopperCartImpactRelay>();
        cartRelay.Initialize(this);

        MonoBehaviour[] behaviours = cart.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == cartRelay)
                continue;

            cartBehaviours.Add(new BehaviourState
            {
                behaviour = behaviours[i],
                wasEnabled = behaviours[i].enabled
            });
        }
    }

    private void BuildRagdoll()
    {
        if (animator == null || !animator.isHuman)
        {
            Debug.LogWarning(name + " needs a Humanoid Avatar to create a shopper ragdoll.", this);
            return;
        }

        for (int i = 0; i < PartBones.Length; i++)
            AddPart(PartBones[i]);

        AddSegment(HumanBodyBones.Hips, HumanBodyBones.Spine, 0.15f);
        AddSegment(HumanBodyBones.Spine, HumanBodyBones.Chest, 0.15f);
        AddSegment(HumanBodyBones.Chest, HumanBodyBones.UpperChest, 0.14f);
        if (GetPart(HumanBodyBones.UpperChest) != null)
            AddSegment(HumanBodyBones.UpperChest, HumanBodyBones.Neck, 0.09f);
        else
            AddSegment(HumanBodyBones.Chest, HumanBodyBones.Neck, 0.09f);
        AddSegment(HumanBodyBones.Neck, HumanBodyBones.Head, 0.105f);
        AddSphere(HumanBodyBones.Head, 0.13f);

        AddSegment(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 0.075f);
        AddSegment(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.06f);
        AddSegment(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.075f);
        AddSegment(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.06f);
        AddSegment(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 0.12f);
        AddSegment(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.085f);
        AddSegment(HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, 0.075f);
        AddSegment(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 0.12f);
        AddSegment(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.085f);
        AddSegment(HumanBodyBones.RightFoot, HumanBodyBones.RightToes, 0.075f);

        for (int i = 0; i < orderedParts.Count; i++)
        {
            Part part = orderedParts[i];
            part.body.isKinematic = true;
            part.body.useGravity = true;
            part.body.detectCollisions = false;
            for (int j = 0; j < part.colliders.Count; j++)
                part.colliders[j].enabled = false;
        }
    }

    private void CreateRagdollJoints()
    {
        for (int i = 0; i < orderedParts.Count; i++)
        {
            Part part = orderedParts[i];
            Part parent = FindParentPart(part.transform.parent);
            if (parent == null)
                continue;

            CharacterJoint joint = part.transform.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent.body;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = parent.transform.InverseTransformPoint(part.transform.position);
            joint.enableProjection = true;
            joint.projectionDistance = 0.08f;
            joint.projectionAngle = 15f;
            joint.lowTwistLimit = new SoftJointLimit { limit = -25f };
            joint.highTwistLimit = new SoftJointLimit { limit = 25f };
            joint.swing1Limit = new SoftJointLimit { limit = 35f };
            joint.swing2Limit = new SoftJointLimit { limit = 35f };
            part.joint = joint;

            for (int childCollider = 0; childCollider < part.colliders.Count; childCollider++)
            {
                for (int parentCollider = 0; parentCollider < parent.colliders.Count; parentCollider++)
                    Physics.IgnoreCollision(part.colliders[childCollider], parent.colliders[parentCollider], true);
            }
        }
    }

    private void AddPart(HumanBodyBones bone)
    {
        if (parts.ContainsKey(bone))
            return;

        Transform boneTransform = animator.GetBoneTransform(bone);
        if (boneTransform == null)
            return;

        float mass = GetMass(bone);
        Rigidbody body = boneTransform.GetComponent<Rigidbody>();
        if (body == null)
            body = boneTransform.gameObject.AddComponent<Rigidbody>();
        body.mass = mass;
        body.useGravity = true;
        body.isKinematic = true;
        body.detectCollisions = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Part part = new Part
        {
            bone = bone,
            transform = boneTransform,
            body = body,
            localPosition = boneTransform.localPosition,
            localRotation = boneTransform.localRotation
        };
        parts.Add(bone, part);
        orderedParts.Add(part);
    }

    private static float GetMass(HumanBodyBones bone)
    {
        switch (bone)
        {
            case HumanBodyBones.Hips: return 2.5f;
            case HumanBodyBones.Spine:
            case HumanBodyBones.Chest:
            case HumanBodyBones.UpperChest: return 1.7f;
            case HumanBodyBones.Head: return 0.9f;
            case HumanBodyBones.LeftUpperArm:
            case HumanBodyBones.RightUpperArm:
            case HumanBodyBones.LeftUpperLeg:
            case HumanBodyBones.RightUpperLeg: return 1.1f;
            case HumanBodyBones.LeftLowerArm:
            case HumanBodyBones.RightLowerArm:
            case HumanBodyBones.LeftLowerLeg:
            case HumanBodyBones.RightLowerLeg: return 0.75f;
            default: return 0.4f;
        }
    }

    private Part FindParentPart(Transform parent)
    {
        while (parent != null && parent != transform)
        {
            for (int i = 0; i < orderedParts.Count; i++)
            {
                if (orderedParts[i].transform == parent)
                    return orderedParts[i];
            }
            parent = parent.parent;
        }
        return null;
    }

    private void AddSegment(HumanBodyBones startBone, HumanBodyBones endBone, float radius)
    {
        Part start = GetPart(startBone);
        Part end = GetPart(endBone);
        if (start == null || end == null)
            return;

        Vector3 delta = end.transform.position - start.transform.position;
        if (delta.sqrMagnitude < 0.0001f)
            return;

        GameObject colliderObject = new GameObject(startBone + "_RagdollCollider");
        colliderObject.layer = start.transform.gameObject.layer;
        colliderObject.transform.SetParent(start.transform, true);
        colliderObject.transform.SetPositionAndRotation(
            (start.transform.position + end.transform.position) * 0.5f,
            Quaternion.FromToRotation(Vector3.up, delta.normalized));

        CapsuleCollider capsule = colliderObject.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.radius = radius;
        capsule.height = Mathf.Max(delta.magnitude + radius * 2f, radius * 2f);
        capsule.enabled = false;
        start.colliders.Add(capsule);
    }

    private void AddSphere(HumanBodyBones bone, float radius)
    {
        Part part = GetPart(bone);
        if (part == null)
            return;

        GameObject colliderObject = new GameObject(bone + "_RagdollCollider");
        colliderObject.layer = part.transform.gameObject.layer;
        colliderObject.transform.SetParent(part.transform, false);
        SphereCollider sphere = colliderObject.AddComponent<SphereCollider>();
        sphere.radius = radius;
        sphere.enabled = false;
        part.colliders.Add(sphere);
    }

    private Part GetPart(HumanBodyBones bone)
    {
        Part part;
        return parts.TryGetValue(bone, out part) ? part : null;
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleImpact(collision);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleTriggerImpact(other);
    }

    public void HandleCartImpact(Collision collision)
    {
        HandleImpact(collision);
    }

    public void HandleCartTriggerImpact(Collider other)
    {
        HandleTriggerImpact(other);
    }

    private void HandleTriggerImpact(Collider other)
    {
        if (ragdollActive || !IsPlayerCollider(other))
            return;

        float impactSpeed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
        if (other.attachedRigidbody != null)
            impactSpeed += other.attachedRigidbody.linearVelocity.magnitude;
        CharacterController controller = other.GetComponentInParent<CharacterController>();
        if (controller != null)
            impactSpeed += controller.velocity.magnitude;
        if (impactSpeed < minimumImpactSpeed)
            return;

        Vector3 away = transform.position - other.bounds.center;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
            away = -transform.forward;
        away.Normalize();
        BeginRagdoll(away, impactSpeed, cart != null ? cart.position : transform.position);
    }

    private void HandleImpact(Collision collision)
    {
        if (ragdollActive || collision == null || collision.collider == null || !IsPlayerCollider(collision.collider))
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < minimumImpactSpeed)
            return;

        Vector3 away = transform.position - collision.collider.bounds.center;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
            away = -transform.forward;
        away.Normalize();

        Vector3 contactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        BeginRagdoll(away, impactSpeed, contactPoint);
    }

    private static bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
            return true;

        if (other.GetComponentInParent<CharacterController>() != null)
            return true;

        Rigidbody body = other.attachedRigidbody;
        return body != null && !body.isKinematic && body.CompareTag("Player");
    }

    private void BeginRagdoll(Vector3 away, float impactSpeed, Vector3 contactPoint)
    {
        if (ragdollActive)
            return;

        ragdollActive = true;
        standingRotation = transform.rotation;

        if (shopper != null)
            shopper.SuspendForRagdoll();

        bodyCollider.enabled = false;
        bumpTrigger.enabled = false;
        rootBody.detectCollisions = false;

        if (cart != null)
        {
            if (cartWasChild && cart.IsChildOf(transform))
                cart.SetParent(null, true);

            for (int i = 0; i < cartBehaviours.Count; i++)
            {
                BehaviourState state = cartBehaviours[i];
                if (state.behaviour != null)
                    state.behaviour.enabled = false;
            }

            if (cartBody != null)
            {
                cartBody.constraints = RigidbodyConstraints.None;
                cartBody.useGravity = true;
                cartBody.isKinematic = false;
                cartBody.detectCollisions = true;
                cartBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        for (int i = 0; i < orderedParts.Count; i++)
        {
            Part part = orderedParts[i];
            part.body.isKinematic = false;
            part.body.detectCollisions = true;
            for (int j = 0; j < part.colliders.Count; j++)
                part.colliders[j].enabled = true;
        }

        CreateRagdollJoints();

        Part torso = GetPart(HumanBodyBones.Chest);
        if (torso == null)
            torso = GetPart(HumanBodyBones.Spine);
        if (torso == null)
            torso = GetPart(HumanBodyBones.Hips);

        float speedBonus = Mathf.Clamp(impactSpeed * 1.2f, 0f, 6f);
        Vector3 shopperImpulse = away * (shopperPushImpulse + speedBonus) + Vector3.up * shopperUpwardImpulse;
        if (torso != null)
            torso.body.AddForceAtPosition(shopperImpulse, contactPoint, ForceMode.Impulse);

        if (cartBody != null)
        {
            Vector3 cartImpulse = away * (cartPushImpulse + speedBonus * 2f) + Vector3.up * cartUpwardImpulse;
            cartBody.AddForceAtPosition(cartImpulse, contactPoint, ForceMode.Impulse);
        }

        StartCoroutine(RecoverAfterDelay());
    }

    private IEnumerator RecoverAfterDelay()
    {
        yield return new WaitForSeconds(recoveryDelay);

        Vector3 recoveryPosition = transform.position;
        Part hips = GetPart(HumanBodyBones.Hips);
        if (hips != null)
            recoveryPosition = hips.transform.position;

        NavMeshHit hit;
        int areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas;
        if (NavMesh.SamplePosition(recoveryPosition, out hit, recoverySearchRadius, areaMask))
            recoveryPosition = hit.position;
        else if (NavMesh.SamplePosition(transform.position, out hit, recoverySearchRadius, areaMask))
            recoveryPosition = hit.position;

        DisableRagdollAndRestorePose();
        transform.SetPositionAndRotation(recoveryPosition, standingRotation);

        if (bodyCollider != null)
            bodyCollider.enabled = bodyColliderWasEnabled;
        if (bumpTrigger != null)
            bumpTrigger.enabled = bumpTriggerWasEnabled;
        if (rootBody != null)
            rootBody.detectCollisions = rootBodyWasDetectingCollisions;

        RestoreCart();

        bool resumed = shopper != null && shopper.ResumeFromRagdoll(recoveryPosition, standingRotation);
        if (!resumed)
        {
            if (animator != null)
                animator.enabled = true;
            if (agent != null)
                agent.enabled = true;
        }

        ragdollActive = false;
    }

    private void DisableRagdollAndRestorePose()
    {
        for (int i = 0; i < orderedParts.Count; i++)
        {
            Part part = orderedParts[i];
            if (part.joint != null)
            {
                Destroy(part.joint);
                part.joint = null;
            }

            for (int j = 0; j < part.colliders.Count; j++)
                part.colliders[j].enabled = false;

            part.body.linearVelocity = Vector3.zero;
            part.body.angularVelocity = Vector3.zero;
            part.body.detectCollisions = false;
            part.body.isKinematic = true;
        }

        for (int i = 0; i < orderedParts.Count; i++)
        {
            Part part = orderedParts[i];
            part.transform.localPosition = part.localPosition;
            part.transform.localRotation = part.localRotation;
        }
    }

    private void RestoreCart()
    {
        if (cart == null)
            return;

        if (cartBody != null)
        {
            cartBody.linearVelocity = Vector3.zero;
            cartBody.angularVelocity = Vector3.zero;
            cartBody.isKinematic = cartBodyWasKinematic;
            cartBody.useGravity = cartBodyUsedGravity;
            cartBody.constraints = cartOriginalConstraints;
            cartBody.detectCollisions = true;
        }

        if (cartWasChild)
        {
            Transform restoreParent = cartOriginalParent != null ? cartOriginalParent : transform;
            cart.SetParent(restoreParent, false);
            cart.localPosition = cartOriginalLocalPosition;
            cart.localRotation = cartOriginalLocalRotation;
            cart.localScale = cartOriginalLocalScale;
        }
        else
        {
            cart.SetPositionAndRotation(
                transform.TransformPoint(cartOriginalLocalPosition),
                transform.rotation * cartOriginalLocalRotation);
        }

        for (int i = 0; i < cartBehaviours.Count; i++)
        {
            BehaviourState state = cartBehaviours[i];
            if (state.behaviour != null)
                state.behaviour.enabled = state.wasEnabled;
        }
    }
}
