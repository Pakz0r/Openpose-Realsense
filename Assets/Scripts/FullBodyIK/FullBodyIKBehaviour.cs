// Copyright (c) 2016 Nora
// Released under the MIT license
// http://opensource.org/licenses/mit-license.php

using UnityEngine;

namespace SA
{
	[ExecuteInEditMode]
	[DisallowMultipleComponent]
	public class FullBodyIKBehaviour : FullBodyIKBehaviourBase
	{
		[SerializeField]
		FullBodyIK _fullBodyIK;

		public override FullBodyIK FullBodyIK
		{
			get
			{
				_fullBodyIK ??= new FullBodyIK();
				return _fullBodyIK;
			}
		}
	}
}