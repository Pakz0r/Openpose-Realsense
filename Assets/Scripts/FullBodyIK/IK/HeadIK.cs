// Copyright (c) 2016 Nora
// Released under the MIT license
// http://opensource.org/licenses/mit-license.php
using UnityEngine;

namespace SA
{
	public partial class FullBodyIK
	{
		public class HeadIK
		{
			Settings _settings;
			InternalValues _internalValues;

			Bone _neckBone;
			Bone _headBone;
			Bone _leftEyeBone;
			Bone _rightEyeBone;

			Effector _headEffector;
			Effector _eyesEffector;

			Quaternion _headEffectorToWorldRotation = Quaternion.identity;
			Quaternion _headToLeftEyeRotation = Quaternion.identity;
			Quaternion _headToRightEyeRotation = Quaternion.identity;

			public HeadIK(FullBodyIK fullBodyIK)
			{
				_settings = fullBodyIK.settings;
				_internalValues = fullBodyIK.internalValues;

				_neckBone = PrepareBone(fullBodyIK.headBones.neck);
				_headBone = PrepareBone(fullBodyIK.headBones.head);
				_leftEyeBone = PrepareBone(fullBodyIK.headBones.leftEye);
				_rightEyeBone = PrepareBone(fullBodyIK.headBones.rightEye);
				_headEffector = fullBodyIK.headEffectors.head;
				_eyesEffector = fullBodyIK.headEffectors.eyes;
			}

			bool _isSyncDisplacementAtLeastOnce;
			bool _isEnabledCustomEyes;

			void SyncDisplacement(FullBodyIK fullBodyIK)
			{
				// Measure bone length.(Using worldPosition)
				// Force execution on 1st time. (Ignore case _settings.syncDisplacement == SyncDisplacement.Disable)
				if (_settings.syncDisplacement == FullBodyIK.SyncDisplacement.Everyframe || !_isSyncDisplacementAtLeastOnce)
				{
					_isSyncDisplacementAtLeastOnce = true;

					if (_headBone != null && _headBone.TransformIsAlive)
					{
						if (_headEffector != null)
						{
							SAFBIKQuatMultInv0(out _headEffectorToWorldRotation, ref _headEffector._defaultRotation, ref _headBone._defaultRotation);
						}
						if (_leftEyeBone != null && _leftEyeBone.TransformIsAlive)
						{
							SAFBIKQuatMultInv0(out _headToLeftEyeRotation, ref _headBone._defaultRotation, ref _leftEyeBone._defaultRotation);
						}
						if (_rightEyeBone != null && _rightEyeBone.TransformIsAlive)
						{
							SAFBIKQuatMultInv0(out _headToRightEyeRotation, ref _headBone._defaultRotation, ref _rightEyeBone._defaultRotation);
						}
					}

					_isEnabledCustomEyes = fullBodyIK.PrepareCustomEyes(ref _headToLeftEyeRotation, ref _headToRightEyeRotation);
				}
			}

			public bool Solve(FullBodyIK fullBodyIK)
			{
				if (_neckBone == null || !_neckBone.TransformIsAlive ||
					_headBone == null || !_headBone.TransformIsAlive ||
					_headBone.ParentBone == null || !_headBone.ParentBone.TransformIsAlive)
				{
					return false;
				}

				SyncDisplacement(fullBodyIK);

				float headPositionWeight = _headEffector.positionEnabled ? _headEffector.positionWeight : 0.0f;
				float eyesPositionWeight = _eyesEffector.positionEnabled ? _eyesEffector.positionWeight : 0.0f;

				if (headPositionWeight <= IKEpsilon && eyesPositionWeight <= IKEpsilon)
				{
					Quaternion parentWorldRotation = _neckBone.ParentBone.WorldRotation;
					SAFBIKQuatMult(out Quaternion parentBaseRotation, ref parentWorldRotation, ref _neckBone.ParentBone._worldToBaseRotation);

					if (_internalValues.resetTransforms)
					{
						SAFBIKQuatMult(out Quaternion tempRotation, ref parentBaseRotation, ref _neckBone._baseToWorldRotation);
						_neckBone.WorldRotation = tempRotation;
					}

					float headRotationWeight = _headEffector.rotationEnabled ? _headEffector.rotationWeight : 0.0f;
					if (headRotationWeight > IKEpsilon)
					{
						Quaternion headEffectorWorldRotation = _headEffector.WorldRotation;
						SAFBIKQuatMult(out Quaternion toRotation, ref headEffectorWorldRotation, ref _headEffectorToWorldRotation);
						if (headRotationWeight < 1.0f - IKEpsilon)
						{
							Quaternion fromRotation;
							if (_internalValues.resetTransforms)
							{
								SAFBIKQuatMult(out fromRotation, ref parentBaseRotation, ref _headBone._baseToWorldRotation);
							}
							else
							{
								fromRotation = _headBone.WorldRotation; // This is able to use _headBone.worldRotation directly.
							}
							_headBone.WorldRotation = Quaternion.Lerp(fromRotation, toRotation, headRotationWeight);
						}
						else
						{
							_headBone.WorldRotation = toRotation;
						}

						HeadRotationLimit();
					}
					else
					{
						if (_internalValues.resetTransforms)
						{
							Quaternion tempRotation;
							SAFBIKQuatMult(out tempRotation, ref parentBaseRotation, ref _headBone._baseToWorldRotation);
							_headBone.WorldRotation = tempRotation;
						}
					}

					if (_internalValues.resetTransforms)
					{
						if (_isEnabledCustomEyes)
						{
							fullBodyIK.ResetCustomEyes();
						}
						else
						{
							ResetEyes();
						}
					}

					return _internalValues.resetTransforms || (headRotationWeight > IKEpsilon);
				}

				InternalSolve(fullBodyIK);
				return true;
			}

			void HeadRotationLimit()
			{
				// Rotation Limit.
				Quaternion tempRotation, headRotation, neckRotation, localRotation;
				tempRotation = _headBone.WorldRotation;
				SAFBIKQuatMult(out headRotation, ref tempRotation, ref _headBone._worldToBaseRotation);
				tempRotation = _neckBone.WorldRotation;
				SAFBIKQuatMult(out neckRotation, ref tempRotation, ref _neckBone._worldToBaseRotation);
				SAFBIKQuatMultInv0(out localRotation, ref neckRotation, ref headRotation);

				SAFBIKMatSetRot(out Matrix3x3 localBasis, ref localRotation);

				Vector3 localDirY = localBasis.column1;
				Vector3 localDirZ = localBasis.column2;

				bool isLimited = false;
				isLimited |= LimitSquareXZ(ref localDirY,
					_internalValues.headIK.headLimitRollTheta.sin,
					_internalValues.headIK.headLimitRollTheta.sin,
					_internalValues.headIK.headLimitPitchUpTheta.sin,
					_internalValues.headIK.headLimitPitchDownTheta.sin);
				isLimited |= LimitSquareXY(ref localDirZ,
					_internalValues.headIK.headLimitYawTheta.sin,
					_internalValues.headIK.headLimitYawTheta.sin,
					_internalValues.headIK.headLimitPitchDownTheta.sin,
					_internalValues.headIK.headLimitPitchUpTheta.sin);

				if (isLimited)
				{
					if (SAFBIKComputeBasisFromYZLockZ(out localBasis, ref localDirY, ref localDirZ))
					{
						SAFBIKMatGetRot(out localRotation, ref localBasis);
						SAFBIKQuatMultNorm3(out headRotation, ref neckRotation, ref localRotation, ref _headBone._baseToWorldRotation);
						_headBone.WorldRotation = headRotation;
					}
				}
			}

			void InternalSolve(FullBodyIK fullBodyIK)
			{
				Quaternion parentWorldRotation = _neckBone.ParentBone.WorldRotation;
				SAFBIKMatSetRotMultInv1(out Matrix3x3 parentBasis, ref parentWorldRotation, ref _neckBone.ParentBone._defaultRotation);
				SAFBIKMatMult(out Matrix3x3 parentBaseBasis, ref parentBasis, ref _internalValues.defaultRootBasis);
				SAFBIKQuatMult(out Quaternion parentBaseRotation, ref parentWorldRotation, ref _neckBone.ParentBone._worldToBaseRotation);

				float headPositionWeight = _headEffector.positionEnabled ? _headEffector.positionWeight : 0.0f;
				float eyesPositionWeight = _eyesEffector.positionEnabled ? _eyesEffector.positionWeight : 0.0f;

				Quaternion neckBonePrevRotation = Quaternion.identity;
				Quaternion headBonePrevRotation = Quaternion.identity;
				Quaternion leftEyeBonePrevRotation = Quaternion.identity;
				Quaternion rightEyeBonePrevRotation = Quaternion.identity;
				if (!_internalValues.resetTransforms)
				{
					neckBonePrevRotation = _neckBone.WorldRotation;
					headBonePrevRotation = _headBone.WorldRotation;
					if (_leftEyeBone != null && _leftEyeBone.TransformIsAlive)
					{
						leftEyeBonePrevRotation = _leftEyeBone.WorldRotation;
					}
					if (_rightEyeBone != null && _rightEyeBone.TransformIsAlive)
					{
						rightEyeBonePrevRotation = _rightEyeBone.WorldRotation;
					}
				}

				// for Neck
				if (headPositionWeight > IKEpsilon)
				{
					SAFBIKMatMult(out Matrix3x3 neckBoneBasis, ref parentBasis, ref _neckBone._localAxisBasis);

					Vector3 yDir = _headEffector.WorldPosition - _neckBone.WorldPosition; // Not use _hidden_worldPosition
					if (SAFBIKVecNormalize(ref yDir))
					{
						SAFBIKMatMultVecInv(out Vector3 localDir, ref neckBoneBasis, ref yDir);

						if (LimitSquareXZ(ref localDir,
							_internalValues.headIK.neckLimitRollTheta.sin,
							_internalValues.headIK.neckLimitRollTheta.sin,
							_internalValues.headIK.neckLimitPitchDownTheta.sin,
							_internalValues.headIK.neckLimitPitchUpTheta.sin))
						{
							SAFBIKMatMultVec(out yDir, ref neckBoneBasis, ref localDir);
						}

						Vector3 xDir = parentBaseBasis.column0;
						Vector3 zDir = parentBaseBasis.column2;
						if (SAFBIKComputeBasisLockY(out neckBoneBasis, ref xDir, ref yDir, ref zDir))
						{
							SAFBIKMatMultGetRot(out Quaternion worldRotation, ref neckBoneBasis, ref _neckBone._boneToWorldBasis);
							if (headPositionWeight < 1.0f - IKEpsilon)
							{
								Quaternion fromRotation;
								if (_internalValues.resetTransforms)
								{
									SAFBIKQuatMult(out fromRotation, ref parentBaseRotation, ref _neckBone._baseToWorldRotation);
								}
								else
								{
									fromRotation = neckBonePrevRotation; // This is able to use _headBone.worldRotation directly.
								}

								_neckBone.WorldRotation = Quaternion.Lerp(fromRotation, worldRotation, headPositionWeight);
							}
							else
							{
								_neckBone.WorldRotation = worldRotation;
							}
						}
					}
				}
				else if (_internalValues.resetTransforms)
				{
					SAFBIKQuatMult(out Quaternion tempRotation, ref parentBaseRotation, ref _neckBone._baseToWorldRotation);
					_neckBone.WorldRotation = tempRotation;
				}

				// for Head / Eyes
				if (eyesPositionWeight <= IKEpsilon)
				{
					float headRotationWeight = _headEffector.rotationEnabled ? _headEffector.rotationWeight : 0.0f;
					if (headRotationWeight > IKEpsilon)
					{
						Quaternion headEffectorWorldRotation = _headEffector.WorldRotation;
						SAFBIKQuatMult(out Quaternion toRotation, ref headEffectorWorldRotation, ref _headEffectorToWorldRotation);
						if (headRotationWeight < 1.0f - IKEpsilon)
						{
							Quaternion fromRotation;
							Quaternion neckBoneWorldRotation = _neckBone.WorldRotation;
							if (_internalValues.resetTransforms)
							{
								SAFBIKQuatMult3(out fromRotation, ref neckBoneWorldRotation, ref _neckBone._worldToBaseRotation, ref _headBone._baseToWorldRotation);
							}
							else
							{
								// Not use _headBone.worldRotation.
								SAFBIKQuatMultNorm3Inv1(out fromRotation, ref neckBoneWorldRotation, ref neckBonePrevRotation, ref headBonePrevRotation);
							}
							_headBone.WorldRotation = Quaternion.Lerp(fromRotation, toRotation, headRotationWeight);
						}
						else
						{
							_headBone.WorldRotation = toRotation;
						}
					}
					else
					{
						if (_internalValues.resetTransforms)
						{
							Quaternion neckBoneWorldRotation = _neckBone.WorldRotation;
							SAFBIKQuatMult3(out Quaternion headBoneWorldRotation, ref neckBoneWorldRotation, ref _neckBone._worldToBaseRotation, ref _headBone._baseToWorldRotation);
							_headBone.WorldRotation = headBoneWorldRotation;
						}
					}

					HeadRotationLimit();

					if (_internalValues.resetTransforms)
					{
						if (_isEnabledCustomEyes)
						{
							fullBodyIK.ResetCustomEyes();
						}
						else
						{
							ResetEyes();
						}
					}

					return;
				}

				{
					Vector3 parentBoneWorldPosition = _neckBone.ParentBone.WorldPosition;
					SAFBIKMatMultVecPreSubAdd(out Vector3 eyesPosition, ref parentBasis, ref _eyesEffector._defaultPosition, ref _neckBone.ParentBone._defaultPosition, ref parentBoneWorldPosition);

					// Note: Not use _eyesEffector._hidden_worldPosition
					Vector3 eyesDir = _eyesEffector.WorldPosition - eyesPosition; // Memo: Not normalize yet.

					Matrix3x3 neckBaseBasis = parentBaseBasis;

					{
						SAFBIKMatMultVecInv(out Vector3 localDir, ref parentBaseBasis, ref eyesDir);

						localDir.y *= _settings.headIK.eyesToNeckPitchRate;
						SAFBIKVecNormalize(ref localDir);

						if (ComputeEyesRange(ref localDir, _internalValues.headIK.eyesTraceTheta.cos))
						{
							if (localDir.y < -_internalValues.headIK.neckLimitPitchDownTheta.sin)
							{
								localDir.y = -_internalValues.headIK.neckLimitPitchDownTheta.sin;
							}
							else if (localDir.y > _internalValues.headIK.neckLimitPitchUpTheta.sin)
							{
								localDir.y = _internalValues.headIK.neckLimitPitchUpTheta.sin;
							}
							localDir.x = 0.0f;
							localDir.z = SAFBIKSqrt(1.0f - localDir.y * localDir.y);
						}

						SAFBIKMatMultVec(out eyesDir, ref parentBaseBasis, ref localDir);

						{
							Vector3 xDir = parentBaseBasis.column0;
							Vector3 yDir = parentBaseBasis.column1;
							Vector3 zDir = eyesDir;

							if (!SAFBIKComputeBasisLockZ(out neckBaseBasis, ref xDir, ref yDir, ref zDir))
							{
								neckBaseBasis = parentBaseBasis; // Failsafe.
							}
						}

						SAFBIKMatMultGetRot(out Quaternion worldRotation, ref neckBaseBasis, ref _neckBone._baseToWorldBasis);
						if (_eyesEffector.positionWeight < 1.0f - IKEpsilon)
						{
							Quaternion neckWorldRotation = Quaternion.Lerp(_neckBone.WorldRotation, worldRotation, _eyesEffector.positionWeight); // This is able to use _neckBone.worldRotation directly.
							_neckBone.WorldRotation = neckWorldRotation;
							SAFBIKMatSetRotMult(out neckBaseBasis, ref neckWorldRotation, ref _neckBone._worldToBaseRotation);
						}
						else
						{
							_neckBone.WorldRotation = worldRotation;
						}
					}

					SAFBIKMatMult(out Matrix3x3 neckBasis, ref neckBaseBasis, ref _internalValues.defaultRootBasisInv);

					Vector3 neckBoneWorldPosition = _neckBone.WorldPosition;
					SAFBIKMatMultVecPreSubAdd(out eyesPosition, ref neckBasis, ref _eyesEffector._defaultPosition, ref _neckBone._defaultPosition, ref neckBoneWorldPosition);

					// Note: Not use _eyesEffector._hidden_worldPosition
					eyesDir = _eyesEffector.WorldPosition - eyesPosition;

					Matrix3x3 headBaseBasis = neckBaseBasis;

					{
						SAFBIKMatMultVecInv(out Vector3 localDir, ref neckBaseBasis, ref eyesDir);

						localDir.x *= _settings.headIK.eyesToHeadYawRate;
						localDir.y *= _settings.headIK.eyesToHeadPitchRate;

						SAFBIKVecNormalize(ref localDir);

						if (ComputeEyesRange(ref localDir, _internalValues.headIK.eyesTraceTheta.cos))
						{
							// Note: Not use _LimitXY() for Stability
							LimitSquareXY(ref localDir,
								_internalValues.headIK.headLimitYawTheta.sin,
								_internalValues.headIK.headLimitYawTheta.sin,
								_internalValues.headIK.headLimitPitchDownTheta.sin,
								_internalValues.headIK.headLimitPitchUpTheta.sin);
						}

						SAFBIKMatMultVec(out eyesDir, ref neckBaseBasis, ref localDir);

						{
							Vector3 xDir = neckBaseBasis.column0;
							Vector3 yDir = neckBaseBasis.column1;
							Vector3 zDir = eyesDir;

							if (!SAFBIKComputeBasisLockZ(out headBaseBasis, ref xDir, ref yDir, ref zDir))
							{
								headBaseBasis = neckBaseBasis;
							}
						}

						SAFBIKMatMultGetRot(out Quaternion worldRotation, ref headBaseBasis, ref _headBone._baseToWorldBasis);
						if (_eyesEffector.positionWeight < 1.0f - IKEpsilon)
						{
							Quaternion neckBoneWorldRotation = _neckBone.WorldRotation;
							Quaternion headFromWorldRotation;
							SAFBIKQuatMultNorm3Inv1(out headFromWorldRotation, ref neckBoneWorldRotation, ref neckBonePrevRotation, ref headBonePrevRotation);
							Quaternion headWorldRotation = Quaternion.Lerp(headFromWorldRotation, worldRotation, _eyesEffector.positionWeight);
							_headBone.WorldRotation = headWorldRotation;
							SAFBIKMatSetRotMult(out headBaseBasis, ref headWorldRotation, ref _headBone._worldToBaseRotation);
						}
						else
						{
							_headBone.WorldRotation = worldRotation;
						}
					}

					SAFBIKMatMult(out Matrix3x3 headBasis, ref headBaseBasis, ref _internalValues.defaultRootBasisInv);

					if (_isEnabledCustomEyes)
					{
						fullBodyIK.SolveCustomEyes(ref neckBasis, ref headBasis, ref headBaseBasis);
					}
					else
					{
						SolveEyes(ref neckBasis, ref headBasis, ref headBaseBasis, ref headBonePrevRotation, ref leftEyeBonePrevRotation, ref rightEyeBonePrevRotation);
					}
				}
			}

			void ResetEyes()
			{
				if (_headBone != null && _headBone.TransformIsAlive)
				{
					Quaternion headWorldRotation = _headBone.WorldRotation;

					Quaternion worldRotation;
					if (_leftEyeBone != null && _leftEyeBone.TransformIsAlive)
					{
						SAFBIKQuatMultNorm(out worldRotation, ref headWorldRotation, ref _headToLeftEyeRotation);
						_leftEyeBone.WorldRotation = worldRotation;
					}
					if (_rightEyeBone != null && _rightEyeBone.TransformIsAlive)
					{
						SAFBIKQuatMultNorm(out worldRotation, ref headWorldRotation, ref _headToRightEyeRotation);
						_rightEyeBone.WorldRotation = worldRotation;
					}
				}
			}

			void SolveEyes(ref Matrix3x3 neckBasis, ref Matrix3x3 headBasis, ref Matrix3x3 headBaseBasis,
				ref Quaternion headPrevRotation, ref Quaternion leftEyePrevRotation, ref Quaternion rightEyePrevRotation)
			{
				if (_headBone != null && _headBone.TransformIsAlive)
				{
					if ((_leftEyeBone != null && _leftEyeBone.TransformIsAlive) || (_rightEyeBone != null && _rightEyeBone.TransformIsAlive))
					{
						Vector3 neckBoneWorldPosition = _neckBone.WorldPosition;
						SAFBIKMatMultVecPreSubAdd(out Vector3 headWorldPosition, ref neckBasis, ref _headBone._defaultPosition, ref _neckBone._defaultPosition, ref neckBoneWorldPosition);

						SAFBIKMatMultVecPreSubAdd(out Vector3 eyesPosition, ref headBasis, ref _eyesEffector._defaultPosition, ref _headBone._defaultPosition, ref headWorldPosition);

						Vector3 eyesDir = _eyesEffector.WorldPosition - eyesPosition;

						SAFBIKMatMultVecInv(out eyesDir, ref headBaseBasis, ref eyesDir);

						SAFBIKVecNormalize(ref eyesDir);

						if (_internalValues.resetTransforms && _eyesEffector.positionWeight < 1.0f - IKEpsilon)
						{
							Vector3 tempDir = Vector3.Lerp(new Vector3(0.0f, 0.0f, 1.0f), eyesDir, _eyesEffector.positionWeight);
							if (SAFBIKVecNormalize(ref tempDir))
							{
								eyesDir = tempDir;
							}
						}

						LimitSquareXY(ref eyesDir,
							_internalValues.headIK.eyesLimitYawTheta.sin,
							_internalValues.headIK.eyesLimitYawTheta.sin,
							_internalValues.headIK.eyesLimitPitchTheta.sin,
							_internalValues.headIK.eyesLimitPitchTheta.sin);

						eyesDir.x *= _settings.headIK.eyesYawRate;
						eyesDir.y *= _settings.headIK.eyesPitchRate;
						Vector3 leftEyeDir = eyesDir;
						Vector3 rightEyeDir = eyesDir;

						if (eyesDir.x >= 0.0f)
						{
							leftEyeDir.x *= _settings.headIK.eyesYawInnerRate;
							rightEyeDir.x *= _settings.headIK.eyesYawOuterRate;
						}
						else
						{
							leftEyeDir.x *= _settings.headIK.eyesYawOuterRate;
							rightEyeDir.x *= _settings.headIK.eyesYawInnerRate;
						}

						SAFBIKVecNormalize2(ref leftEyeDir, ref rightEyeDir);

						SAFBIKMatMultVec(out leftEyeDir, ref headBaseBasis, ref leftEyeDir);
						SAFBIKMatMultVec(out rightEyeDir, ref headBaseBasis, ref rightEyeDir);

						Quaternion worldRotation;

						Quaternion headBoneWorldRotation = _headBone.WorldRotation;

						if (_leftEyeBone != null && _leftEyeBone.TransformIsAlive)
						{
							SAFBIKComputeBasisLockZ(out Matrix3x3 leftEyeBaseBasis, ref headBasis.column0, ref headBasis.column1, ref leftEyeDir);
							SAFBIKMatMultGetRot(out worldRotation, ref leftEyeBaseBasis, ref _leftEyeBone._baseToWorldBasis);
							if (!_internalValues.resetTransforms && _eyesEffector.positionWeight < 1.0f - IKEpsilon)
							{
								SAFBIKQuatMultNorm3Inv1(out Quaternion fromRotation, ref headBoneWorldRotation, ref headPrevRotation, ref leftEyePrevRotation);
								_leftEyeBone.WorldRotation = Quaternion.Lerp(fromRotation, worldRotation, _eyesEffector.positionWeight);
							}
							else
							{
								_leftEyeBone.WorldRotation = worldRotation;
							}
						}

						if (_rightEyeBone != null && _rightEyeBone.TransformIsAlive)
						{
							SAFBIKComputeBasisLockZ(out Matrix3x3 rightEyeBaseBasis, ref headBasis.column0, ref headBasis.column1, ref rightEyeDir);
							SAFBIKMatMultGetRot(out worldRotation, ref rightEyeBaseBasis, ref _rightEyeBone._baseToWorldBasis);
							if (!_internalValues.resetTransforms && _eyesEffector.positionWeight < 1.0f - IKEpsilon)
							{
								SAFBIKQuatMultNorm3Inv1(out Quaternion fromRotation, ref headBoneWorldRotation, ref headPrevRotation, ref rightEyePrevRotation);
								_rightEyeBone.WorldRotation = Quaternion.Lerp(fromRotation, worldRotation, _eyesEffector.positionWeight);
							}
							else
							{
								_rightEyeBone.WorldRotation = worldRotation;
							}
						}
					}
				}

			}
		}
	}
}