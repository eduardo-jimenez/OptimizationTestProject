using Unity.Entities;
using Unity.Mathematics;


namespace ECS
{

	public struct BoidData : IComponentData
	{
		public float2 pos;
		public float2 vel;
		public int cellIndex;
	}

	public struct BoidInCellInfoPlusDist
	{
		public Entity entity;
		public float2 pos;
		public float2 dir;
		public float distance;
	}

}
