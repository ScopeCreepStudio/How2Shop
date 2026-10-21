using UnityEngine;

/// <summary>Gives the stationary cashier a quick arm sweep whenever an item is scanned.</summary>
public sealed class CashierScanAnimation : MonoBehaviour
{
    [SerializeField] private float swipeDuration = 0.28f;

    private Animator animator;
    private Transform upperArm;
    private Transform lowerArm;
    private Transform hand;
    private float swipeTime;
    private bool swiping;
    private float sweepSign = 1f;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>(true);
        if (animator == null)
            return;

        animator.applyRootMotion = false;
        upperArm = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
        lowerArm = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightLowerArm) : null;
        hand = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;

        if (upperArm == null)
            upperArm = FindNamedBone("mixamorig:RightArm");
        if (lowerArm == null)
            lowerArm = FindNamedBone("mixamorig:RightForeArm");
        if (hand == null)
            hand = FindNamedBone("mixamorig:RightHand");
    }

public void PlaySwipe(Vector3 worldDirection, float duration)
{
    Vector3 localDirection = transform.InverseTransformDirection(worldDirection);
    sweepSign = localDirection.x < 0f ? -1f : 1f;
    swipeDuration = Mathf.Max(0.08f, duration);
    swipeTime = 0f;
    swiping = true;
}

    private void LateUpdate()
    {
        if (!swiping)
            return;

        swipeTime += Time.deltaTime;
        float progress = Mathf.Clamp01(swipeTime / Mathf.Max(0.08f, swipeDuration));
        float envelope = Mathf.Sin(progress * Mathf.PI);

        // Animator has already posed the rig this frame, so these offsets layer on
        // top of its idle pose and naturally return when the swipe finishes.
        if (upperArm != null)
            upperArm.localRotation *= Quaternion.Euler(-28f * envelope, 0f, 82f * sweepSign * envelope);
        if (lowerArm != null)
            lowerArm.localRotation *= Quaternion.Euler(8f * envelope, 0f, -48f * sweepSign * envelope);
        if (hand != null)
            hand.localRotation *= Quaternion.Euler(0f, 0f, 18f * sweepSign * envelope);

        if (progress >= 1f)
            swiping = false;
    }

    private Transform FindNamedBone(string boneName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == boneName)
                return child;
        }

        return null;
    }
}
