// Copyright (c) 2016 Nora
// Released under the MIT license
// http://opensource.org/licenses/mit-license.php

using UnityEngine;
using System.Collections.Generic;

namespace SA
{
	public abstract class FullBodyIKBehaviourBase : MonoBehaviour
	{
		[System.NonSerialized]
		FullBodyIK _cache_fullBodyIK; // instance cache

		public abstract FullBodyIK FullBodyIK
		{
			get;
		}

		// Excecutable in Inspector.
		public virtual void Prefix()
		{
			_cache_fullBodyIK ??= FullBodyIK;
			_cache_fullBodyIK?.Prefix(this.transform);
		}

		protected virtual void Awake()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				return;
			}
#endif
			_cache_fullBodyIK ??= FullBodyIK;
			_cache_fullBodyIK?.Awake(this.transform);
		}

		protected virtual void OnDestroy()
		{
			_cache_fullBodyIK ??= FullBodyIK;
			_cache_fullBodyIK?.Destroy();
		}

		protected virtual void LateUpdate()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				return;
			}
#endif
			_cache_fullBodyIK ??= FullBodyIK;
			_cache_fullBodyIK?.Update();
		}

#if UNITY_EDITOR
		void OnDrawGizmos()
		{
			_cache_fullBodyIK ??= FullBodyIK;
			_cache_fullBodyIK?.DrawGizmos();
		}
#endif
	}
}