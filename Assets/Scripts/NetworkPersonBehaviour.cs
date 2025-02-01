using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class NetworkPersonBehaviour : NetworkBehaviour
{
    private Animator animator;
    private NetworkVariable<bool> animatorFallParameter = new();

    #region Unity Lifecycle
    public override void OnNetworkSpawn()
    {
        animator = this.GetComponent<Animator>();

        if (IsServer)
        {
            // sync variables with clients
            SetHasFallen(animator.GetBool("Fall"));
            return;
        }

        // only if application is running on client register handlers

        animatorFallParameter.OnValueChanged -= OnNetworkAnimatorHasFallParameterChange;
        animatorFallParameter.OnValueChanged += OnNetworkAnimatorHasFallParameterChange;
    }

    public override void OnNetworkDespawn()
    {
        animatorFallParameter.OnValueChanged -= OnNetworkAnimatorHasFallParameterChange;
    }
    #endregion

    #region Public Methods
    public void SetHasFallen(bool value)
    {
        if (!IsServer) return; // this method can only be called on server
        animatorFallParameter.Value = value;
        animator.SetBool("Fall", value);
    }
    #endregion

    #region Private Methods
    private void OnNetworkAnimatorHasFallParameterChange(bool previous, bool current)
    {
        Debug.Log($"Detected NetworkVariable 'hasFallen' Change: Previous: {previous} | Current: {current}");
        animator.SetBool("Fall", current);
    }
    #endregion
}
