using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Unity.Mathematics;


namespace ECS
{

	public struct BoidData : IComponentData
	{
		public float2 vel;
	}

}
