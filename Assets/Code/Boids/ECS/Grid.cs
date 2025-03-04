using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;


namespace ECS
{

	public struct GridInfo : IComponentData
	{
		public float2 min;
		public float2 max;
		public float2 size;
		public int2 gridSize;

		public bool initialized;
	}

	public struct GridCellInfo : IComponentData
	{
		public int index;
		public float2 min;
		public float2 max;
	}

	public class GridData : IComponentData
	{
		public NativeArray<Entity> cells;
	}

	public struct BoidInCellBufferData : IBufferElementData
	{
		public Entity boid;
		public float2 pos;
		public float2 dir;
	}

}
