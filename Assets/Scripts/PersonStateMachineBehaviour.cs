using Cysharp.Threading.Tasks;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class PersonStateMachineBehaviour : StateMachineBehaviour
{
    private static float targetRigWeight;
    private static readonly float updateWeightTransitionTime = 1.0f;

    // OnStateEnter is called before OnStateEnter is called on any state inside this state machine
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var isIdle = stateInfo.IsName("Idle");
        var isFallen = stateInfo.IsName("Fallen");

        if (isIdle || isFallen)
        {
            targetRigWeight = isIdle ? 1.0f : 0.0f;
            UpdateRigWeight(animator.gameObject).Forget();
        }
    }

    // Control the animation rigging weight parameter on animation state exit (to be executed on main thread cause unity JOB system)
    async UniTask UpdateRigWeight(GameObject root)
    {
        var rigs = root.GetComponentsInChildren<Rig>();

        if (rigs == null || rigs.Length == 0) return;

        float start = rigs.First().weight;
        float elapsedTime = 0;

        while (elapsedTime < updateWeightTransitionTime)
        {
            await UniTask.Yield();
            var weight = Mathf.Lerp(start, targetRigWeight, (elapsedTime / updateWeightTransitionTime));

            foreach (var rig in rigs)
            {
                rig.weight = weight;
            }

            elapsedTime += Time.deltaTime;
        }
    }
}
