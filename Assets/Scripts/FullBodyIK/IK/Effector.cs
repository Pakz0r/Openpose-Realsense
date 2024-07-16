// Copyright (c) 2016 Nora
// Released under the MIT license
// http://opensource.org/licenses/mit-license.php

#if SAFULLBODYIK_DEBUG
//#define _FORCE_CANCEL_FEEDBACK_WORLDTRANSFORM
#endif

using UnityEngine;

namespace SA
{

	public partial class FullBodyIK
	{
		[System.Serializable]
		public class Effector
		{
			[System.Flags]
			enum EffectorFlags
			{
				None = 0x00,
				RotationContained = 0x01, // Hips/Wrist/Foot
				PullContained = 0x02, // Foot/Wrist
			}

			// Memo: If transform is created & cloned this instance, will be cloned effector transform, too.
			public Transform transform = null;

			public bool positionEnabled = false;
			public bool rotationEnabled = false;
			public float positionWeight = 1.0f;
			public float rotationWeight = 1.0f;
			public float pull = 0.0f;

			[System.NonSerialized]
			public Vector3 _hidden_worldPosition = Vector3.zero;

			public bool EffectorEnabled
			{
				get
				{
					return this.positionEnabled || (this.RotationContained && this.RotationContained);
				}
			}

			[SerializeField]
			bool _isPresetted = false;
			[SerializeField]
			EffectorLocation _effectorLocation = EffectorLocation.Unknown;
			[SerializeField]
			EffectorType _effectorType = EffectorType.Unknown;
			[SerializeField]
			EffectorFlags _effectorFlags = EffectorFlags.None;

			// These aren't serialize field.
			// Memo: If this instance is cloned, will be copyed these properties, too.
			Effector _parentEffector = null;
			Bone _bone = null; // Hips : Hips Eyes : Head
			Bone _leftBone = null; // Hips : LeftLeg Eyes : LeftEye Others : null
			Bone _rightBone = null; // Hips : RightLeg Eyes : RightEye Others : null

			// Memo: If transform is created & cloned this instance, will be cloned effector transform, too.
			[SerializeField]
			Transform _createdTransform = null; // Hidden, for destroy check.

			// Memo: defaultPosition / defaultRotation is copied from bone.
			[SerializeField]
			public Vector3 _defaultPosition = Vector3.zero;
			[SerializeField]
			public Quaternion _defaultRotation = Quaternion.identity;

			public bool _isSimulateFingerTips = false; // Bind effector fingerTips2

			// Basiclly flags.
			public bool RotationContained { get { return (this._effectorFlags & EffectorFlags.RotationContained) != EffectorFlags.None; } }
			public bool PullContained { get { return (this._effectorFlags & EffectorFlags.PullContained) != EffectorFlags.None; } }

			// These are read only properties.
			public EffectorLocation EffectorLocation { get { return _effectorLocation; } }
			public EffectorType EffectorType { get { return _effectorType; } }
			public Effector ParentEffector { get { return _parentEffector; } }
			public Bone Bone { get { return _bone; } }
			public Bone LeftBone { get { return _leftBone; } }
			public Bone RightBone { get { return _rightBone; } }
			public Vector3 DefaultPosition { get { return _defaultPosition; } }
			public Quaternion DefaultRotation { get { return _defaultRotation; } }

			// Internal values. Acepted public accessing. Because these values are required for OnDrawGizmos.
			// (For debug only. You must use worldPosition / worldRotation in useful case.)
			[System.NonSerialized]
			public Vector3 _worldPosition = Vector3.zero;
			[System.NonSerialized]
			public Quaternion _worldRotation = Quaternion.identity;

			// Internal flags.
			bool _isReadWorldPosition = false;
			bool _isReadWorldRotation = false;
			bool _isWrittenWorldPosition = false;
			bool _isWrittenWorldRotation = false;

			bool _isHiddenEyes = false;

			int _transformIsAlive = -1;

			public string name
			{
				get
				{
					return GetEffectorName(_effectorLocation);
				}
			}

			public bool TransformIsAlive
			{
				get
				{
					if (_transformIsAlive == -1)
					{
						_transformIsAlive = CheckAlive(ref this.transform) ? 1 : 0;
					}

					return _transformIsAlive != 0;
				}
			}

			bool DefaultLocalBasisIsIdentity
			{
				get
				{
					if ((_effectorFlags & EffectorFlags.RotationContained) != EffectorFlags.None)
					{ // Hips, Wrist, Foot
						Assert(_bone != null);
						if (_bone != null && _bone.LocalAxisFrom != _LocalAxisFrom.None && _bone.BoneType != BoneType.Hips)
						{ // Exclude Hips.
						  // Hips is identity transform.
							return false;
						}
					}

					return true;
				}
			}

			public void Prefix()
			{
				positionEnabled = GetPresetPositionEnabled(_effectorType);
				positionWeight = GetPresetPositionWeight(_effectorType);
				pull = GetPresetPull(_effectorType);
			}

			void _PresetEffectorLocation(EffectorLocation effectorLocation)
			{
				_isPresetted = true;
				_effectorLocation = effectorLocation;
				_effectorType = ToEffectorType(effectorLocation);
				_effectorFlags = GetEffectorFlags(_effectorType);
			}

			// Call from Awake() or Editor Scripts.
			// Memo: bone.transform is null yet.
			public static void Prefix(
				Effector[] effectors,
				ref Effector effector,
				EffectorLocation effectorLocation,
				bool createEffectorTransform,
				Transform parentTransform,
				Effector parentEffector = null,
				Bone bone = null,
				Bone leftBone = null,
				Bone rightBone = null)
			{
				effector ??= new Effector();

				if (!effector._isPresetted ||
					effector._effectorLocation != effectorLocation ||
					(int)effector._effectorType < 0 ||
					(int)effector._effectorType >= (int)EffectorType.Max)
				{
					effector._PresetEffectorLocation(effectorLocation);
				}

				effector._parentEffector = parentEffector;
				effector._bone = bone;
				effector._leftBone = leftBone;
				effector._rightBone = rightBone;

				// Create or destroy effectorTransform.
				effector.PrefixTransform(createEffectorTransform, parentTransform);

				if (effectors != null)
				{
					effectors[(int)effectorLocation] = effector;
				}
			}

			static bool GetPresetPositionEnabled(EffectorType effectorType)
			{
				return effectorType switch
				{
					EffectorType.Wrist => true,
					EffectorType.Foot => true,
					_ => false,
				};
			}

			static float GetPresetPositionWeight(EffectorType effectorType)
			{
				return effectorType switch
				{
					EffectorType.Arm => 0.0f,
					_ => 1.0f,
				};
			}

			static float GetPresetPull(EffectorType effectorType)
			{
				return effectorType switch
				{
					EffectorType.Hips => 1.0f,
					EffectorType.Eyes => 1.0f,
					EffectorType.Arm => 1.0f,
					EffectorType.Wrist => 1.0f,
					EffectorType.Foot => 1.0f,
					_ => 0.0f,
				};
			}

			static EffectorFlags GetEffectorFlags(EffectorType effectorType)
			{
				return effectorType switch
				{
					EffectorType.Hips => EffectorFlags.RotationContained | EffectorFlags.PullContained,
					EffectorType.Neck => EffectorFlags.PullContained,
					EffectorType.Head => EffectorFlags.RotationContained | EffectorFlags.PullContained,
					EffectorType.Eyes => EffectorFlags.PullContained,
					EffectorType.Arm => EffectorFlags.PullContained,
					EffectorType.Wrist => EffectorFlags.RotationContained | EffectorFlags.PullContained,
					EffectorType.Foot => EffectorFlags.RotationContained | EffectorFlags.PullContained,
					EffectorType.Elbow => EffectorFlags.PullContained,
					EffectorType.Knee => EffectorFlags.PullContained,
					_ => EffectorFlags.None,
				};
			}

			void PrefixTransform(bool createEffectorTransform, Transform parentTransform)
			{
				if (createEffectorTransform)
				{
					if (this.transform == null || this.transform != _createdTransform)
					{
						if (this.transform == null)
						{
							var go = new GameObject(GetEffectorName(_effectorLocation));
							if (parentTransform != null)
							{
								go.transform.SetParent(parentTransform, false);
							}
							else if (_parentEffector != null && _parentEffector.TransformIsAlive)
							{
								go.transform.SetParent(_parentEffector.transform, false);
							}
							this.transform = go.transform;
							this._createdTransform = go.transform;
						}
						else
						{ // Cleanup created transform.
							DestroyImmediate(ref _createdTransform, true);
						}
					}
					else
					{
						CheckAlive(ref _createdTransform); // Overwrite weak reference.
					}
				}
				else
				{ // Cleanup created transform.
					if (_createdTransform != null)
					{
						if (this.transform == _createdTransform)
						{
							this.transform = null;
						}
						Object.DestroyImmediate(_createdTransform.gameObject, true);
					}
					_createdTransform = null; // Overwrite weak reference.
				}

				_transformIsAlive = CheckAlive(ref this.transform) ? 1 : 0;
			}

			public void Prepare(FullBodyIK fullBodyIK)
			{
				Assert(fullBodyIK != null);

				ClearInternal();

				ComputeDefaultTransform(fullBodyIK);

				// Reset transform.
				if (this.TransformIsAlive)
				{
					if (_effectorType == EffectorType.Eyes)
					{
						this.transform.position = _defaultPosition + fullBodyIK.internalValues.defaultRootBasis.column2 * Eyes_DefaultDistance;
					}
					else
					{
						this.transform.position = _defaultPosition;
					}

					if (!DefaultLocalBasisIsIdentity)
					{
						this.transform.rotation = _defaultRotation;
					}
					else
					{
						this.transform.localRotation = Quaternion.identity;
					}

					this.transform.localScale = Vector3.one;
				}

				_worldPosition = _defaultPosition;
				_worldRotation = _defaultRotation;
				if (_effectorType == EffectorType.Eyes)
				{
					_worldPosition += fullBodyIK.internalValues.defaultRootBasis.column2 * Eyes_DefaultDistance;
				}
			}

			public void ComputeDefaultTransform(FullBodyIK fullBodyIK)
			{
				if (_parentEffector != null)
				{
					_defaultRotation = _parentEffector._defaultRotation;
				}

				if (_effectorType == EffectorType.Root)
				{
					_defaultPosition = fullBodyIK.internalValues.defaultRootPosition;
					_defaultRotation = fullBodyIK.internalValues.defaultRootRotation;
				}
				else if (_effectorType == EffectorType.HandFinger)
				{
					Assert(_bone != null);
					if (_bone != null)
					{
						if (_bone.TransformIsAlive)
						{
							_defaultPosition = Bone._defaultPosition;
						}
						else
						{ // Failsafe. Simulate finger tips.
						  // Memo: If transformIsAlive == false, _parentBone is null.
							Assert(_bone.ParentBoneLocationBased != null && _bone.ParentBoneLocationBased.ParentBoneLocationBased != null);
							if (_bone.ParentBoneLocationBased != null && _bone.ParentBoneLocationBased.ParentBoneLocationBased != null)
							{
								Vector3 tipTranslate = (Bone.ParentBoneLocationBased._defaultPosition - Bone.ParentBoneLocationBased.ParentBoneLocationBased._defaultPosition);
								_defaultPosition = Bone.ParentBoneLocationBased._defaultPosition + tipTranslate;
								_isSimulateFingerTips = true;
							}
						}
					}
				}
				else if (_effectorType == EffectorType.Eyes)
				{
					Assert(_bone != null);
					_isHiddenEyes = fullBodyIK.IsHiddenCustomEyes();
					if (!_isHiddenEyes && _bone != null && _bone.TransformIsAlive &&
						_leftBone != null && _leftBone.TransformIsAlive &&
						_rightBone != null && _rightBone.TransformIsAlive)
					{
						// _bone ... Head / _leftBone ... LeftEye / _rightBone ... RightEye
						_defaultPosition = (_leftBone._defaultPosition + _rightBone._defaultPosition) * 0.5f;
					}
					else if (_bone != null && _bone.TransformIsAlive)
					{
						_defaultPosition = _bone._defaultPosition;
						// _bone ... Head / _bone.parentBone ... Neck
						if (_bone.ParentBone != null && _bone.ParentBone.TransformIsAlive && _bone.ParentBone.BoneType == BoneType.Neck)
						{
							Vector3 neckToHead = _bone._defaultPosition - _bone.ParentBone._defaultPosition;
							float neckToHeadY = (neckToHead.y > 0.0f) ? neckToHead.y : 0.0f;
							_defaultPosition += fullBodyIK.internalValues.defaultRootBasis.column1 * neckToHeadY;
							_defaultPosition += fullBodyIK.internalValues.defaultRootBasis.column2 * neckToHeadY;
						}
					}
				}
				else if (_effectorType == EffectorType.Hips)
				{
					Assert(_bone != null && _leftBone != null && _rightBone != null);
					if (_bone != null && _leftBone != null && _rightBone != null)
					{
						// _bone ... Hips / _leftBone ... LeftLeg / _rightBone ... RightLeg
						_defaultPosition = (_leftBone._defaultPosition + _rightBone._defaultPosition) * 0.5f;
					}
				}
				else
				{ // Normally case.
					Assert(_bone != null);
					if (_bone != null)
					{
						_defaultPosition = Bone._defaultPosition;
						if (!DefaultLocalBasisIsIdentity)
						{ // For wrist & foot.
							_defaultRotation = Bone._localAxisRotation;
						}
					}
				}
			}

			void ClearInternal()
			{
				_transformIsAlive = -1;
				_defaultPosition = Vector3.zero;
				_defaultRotation = Quaternion.identity;
			}

			public void PrepareUpdate()
			{
				_transformIsAlive = -1;
				_isReadWorldPosition = false;
				_isReadWorldRotation = false;
				_isWrittenWorldPosition = false;
				_isWrittenWorldRotation = false;
			}

			public Vector3 WorldPosition
			{
				get
				{
					if (!_isReadWorldPosition && !_isWrittenWorldPosition)
					{
						_isReadWorldPosition = true;
						if (this.TransformIsAlive)
						{
							_worldPosition = this.transform.position;
						}
					}
					return _worldPosition;
				}
				set
				{
					_isWrittenWorldPosition = true;
					_worldPosition = value;
				}
			}

			public Vector3 BoneWorldPosition
			{
				get
				{
					if (_effectorType == EffectorType.Eyes)
					{
						if (!_isHiddenEyes && _bone != null && _bone.TransformIsAlive &&
							_leftBone != null && _leftBone.TransformIsAlive &&
							_rightBone != null && _rightBone.TransformIsAlive)
						{
							// _bone ... Head / _leftBone ... LeftEye / _rightBone ... RightEye
							return (_leftBone.WorldPosition + _rightBone.WorldPosition) * 0.5f;
						}
						else if (_bone != null && _bone.TransformIsAlive)
						{
							Vector3 currentPosition = _bone.WorldPosition;
							// _bone ... Head / _bone.parentBone ... Neck
							if (_bone.ParentBone != null && _bone.ParentBone.TransformIsAlive && _bone.ParentBone.BoneType == BoneType.Neck)
							{
								Vector3 neckToHead = _bone._defaultPosition - _bone.ParentBone._defaultPosition;
								float neckToHeadY = (neckToHead.y > 0.0f) ? neckToHead.y : 0.0f;
								Quaternion parentBaseRotation = (_bone.ParentBone.WorldRotation * _bone.ParentBone._worldToBaseRotation);
								SAFBIKMatSetRot(out Matrix3x3 parentBaseBasis, ref parentBaseRotation);
								currentPosition += parentBaseBasis.column1 * neckToHeadY;
								currentPosition += parentBaseBasis.column2 * neckToHeadY;
							}
							return currentPosition;
						}
					}
					else if (_isSimulateFingerTips)
					{
						if (_bone != null &&
							_bone.ParentBoneLocationBased != null &&
							_bone.ParentBoneLocationBased.TransformIsAlive &&
							_bone.ParentBoneLocationBased.ParentBoneLocationBased != null &&
							_bone.ParentBoneLocationBased.ParentBoneLocationBased.TransformIsAlive)
						{
							Vector3 parentPosition = _bone.ParentBoneLocationBased.WorldPosition;
							Vector3 parentParentPosition = _bone.ParentBoneLocationBased.ParentBoneLocationBased.WorldPosition;
							return parentPosition + (parentPosition - parentParentPosition);
						}
					}
					else
					{
						if (_bone != null && _bone.TransformIsAlive)
						{
							return _bone.WorldPosition;
						}
					}

					return this.WorldPosition; // Failsafe.
				}
			}

			public Quaternion WorldRotation
			{
				get
				{
					if (!_isReadWorldRotation && !_isWrittenWorldRotation)
					{
						_isReadWorldRotation = true;
						if (this.TransformIsAlive)
						{
							_worldRotation = this.transform.rotation;
						}
					}
					return _worldRotation;
				}
				set
				{
					_isWrittenWorldRotation = true;
					_worldRotation = value;
				}
			}

			public void WriteToTransform()
			{
#if _FORCE_CANCEL_FEEDBACK_WORLDTRANSFORM
				// Nothing.
#else
				if (_isWrittenWorldPosition)
				{
					_isWrittenWorldPosition = false; // Turn off _isWrittenWorldPosition
					if (this.TransformIsAlive)
					{
						this.transform.position = _worldPosition;
					}
				}
				if (_isWrittenWorldRotation)
				{
					_isWrittenWorldRotation = false; // Turn off _isWrittenWorldRotation
					if (this.TransformIsAlive)
					{
						this.transform.rotation = _worldRotation;
					}
				}
#endif
			}
		}
	}
}