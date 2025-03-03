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

	public struct BoidInCellInfoPlusDist
	{
		public Entity entity;
		public float2 pos;
		public float2 dir;
		public float distance;
	}

}
