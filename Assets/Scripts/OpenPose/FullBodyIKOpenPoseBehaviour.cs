using UnityEngine;
using SA;

[ExecuteInEditMode]
[DisallowMultipleComponent]
public class FullBodyIKOpenPoseBehaviour : FullBodyIKBehaviourBase
{
    [SerializeField]
    FullBodyIKOpenPose _fullBodyIK;

    public override FullBodyIK FullBodyIK
    {
        get
        {
            if (_fullBodyIK == null)
            {
                _fullBodyIK = new FullBodyIKOpenPose();
            }

            return _fullBodyIK;
        }
    }
}