using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Profiling;


namespace ECS
{

    [UpdateBefore(typeof(BoidsSystem))]
	[BurstCompile]
	public partial struct GridSystem : ISystem
	{
        public const int DefaultBoidsInCellBufferCapacity = 64;

        private EntityQuery boidsQuery;
        private EntityQuery cellsQuery;

        public void OnCreate(ref SystemState state)
		{
            boidsQuery = new EntityQueryBuilder(AllocatorManager.Persistent).WithAll<BoidData, LocalTransform>().Build(ref state);
            cellsQuery = new EntityQueryBuilder(AllocatorManager.Persistent).WithAll<GridCellInfo>().Build(ref state);

            state.RequireForUpdate<GridInfo>();
		}

		public void OnDestroy(ref SystemState state)
		{
            // get rid of the cells hash map
            if (SystemAPI.ManagedAPI.TryGetSingleton<GridData>(out GridData gridData) &&
                gridData.cells.IsCreated)
            {
                gridData.cells.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
		{
            // first we have to check if we have to initialize the grid
            Entity gridEntity = SystemAPI.GetSingletonEntity<GridInfo>();
            GridInfo gridInfo = SystemAPI.GetComponent<GridInfo>(gridEntity);
            GridData gridData = SystemAPI.ManagedAPI.GetComponent<GridData>(gridEntity);
			if (!gridData.initialized)
				CreateGrid(ref state, gridEntity, gridInfo, gridData);

            Profiler.BeginSample("Clear Buffers");

            //var clearECB = new EntityCommandBuffer(state.WorldUnmanaged.UpdateAllocator.ToAllocator);

            ClearBuffersJob clearJob = new ClearBuffersJob
            {
                //ecb = clearECB.AsParallelWriter(),
                boidInCellLookup = SystemAPI.GetBufferLookup<BoidInCellBufferData>(false),
            };

            // execute the job
            //JobHandle clearJobHandle = clearJob.ScheduleParallel(cellsQuery, state.Dependency);
            JobHandle clearJobHandle = clearJob.Schedule(cellsQuery, state.Dependency);
            clearJobHandle.Complete();

            // execute and dispose the command buffer
            //clearECB.Playback(state.EntityManager);
            //clearECB.Dispose();

            Profiler.EndSample();

            Profiler.BeginSample("Distribute Boids");

            // now let's update it placing each boid in their cell
            //var ecb = new EntityCommandBuffer(state.WorldUnmanaged.UpdateAllocator.ToAllocator);
            DistributeBoidsInCellsJob job = new DistributeBoidsInCellsJob
            {
                cells = gridData.cells,
                cellLookup = SystemAPI.GetComponentLookup<GridCellInfo>(true),
                boidInCellLookup = SystemAPI.GetBufferLookup<BoidInCellBufferData>(false),

                boidInfo = SystemAPI.GetSingleton<BoidBehaviourInfo>(),

                //ecb = ecb.AsParallelWriter(),

                boundsMin = gridInfo.min,
                boundsMax = gridInfo.max,
                boundsSize = gridInfo.size,
                gridSize = gridInfo.gridSize,
            };

            // execute the job
            JobHandle jobHandle = job.Schedule/*Parallel*/(boidsQuery, clearJobHandle);
            jobHandle.Complete();

            // execute and dispose the command buffer
            //ecb.Playback(state.EntityManager);
            //ecb.Dispose();

            Profiler.EndSample();
		}

        #region Creation Methods

		private void CreateGrid(ref SystemState state, Entity gridEntity, in GridInfo gridInfo, GridData gridData)
		{
            // create the array of cells in the grid data
            int numCells = gridInfo.gridSize.x * gridInfo.gridSize.y;
            if (gridData.cells.IsCreated)
                gridData.cells.Dispose();
            gridData.cells = new NativeArray<Entity>(numCells, Allocator.Persistent);

            // get the bounds where the boids exist
            float2 boundsSize = gridInfo.max - gridInfo.min;

            // create the cells
            for (int iy = 0; iy < gridInfo.gridSize.y; ++iy)
            {
                float ty0 = (float)iy / (float)gridInfo.gridSize.y;
                float ty1 = (float)(iy + 1) / (float)gridInfo.gridSize.y;
                float minY = Mathf.Lerp(gridInfo.min.y, gridInfo.max.y, ty0);
                float maxY = Mathf.Lerp(gridInfo.min.y, gridInfo.max.y, ty1);

                for (int ix = 0; ix < gridInfo.gridSize.x; ++ix)
                {
                    float tx0 = (float)ix / (float)gridInfo.gridSize.x;
                    float tx1 = (float)(ix + 1) / (float)gridInfo.gridSize.x;
                    float minX = Mathf.Lerp(gridInfo.min.x, gridInfo.max.x, tx0);
                    float maxX = Mathf.Lerp(gridInfo.min.x, gridInfo.max.x, tx1);

                    int index = GetIndex(ix, iy, gridInfo.gridSize);

                    // create the cell
                    Entity entity = state.EntityManager.CreateEntity();
                    state.EntityManager.AddComponentData<GridCellInfo>(entity, new GridCellInfo
                    {
                        index = index,
                        min = new float2(minX, minY),
                        max = new float2(maxX, maxY),
                    });
                    var dynBuffer = state.EntityManager.AddBuffer<BoidInCellBufferData>(entity);
                    dynBuffer.Capacity = DefaultBoidsInCellBufferCapacity;
                    dynBuffer.Length = 0;

                    // add the cell to the grid data
                    gridData.cells[index] = entity;
                }
            }

            // mark as initialized
            gridData.initialized = true;

            Debug.Log($"Finished initializing the grid with {numCells} cells");
		}

        #endregion

        #region Helper Methods

        /// <summary>
        /// Returns the index to use for the given position in the grid
        /// </summary>
        public int GetIndex(int x, int y, int2 gridSize)
        {
            return y * gridSize.x + x;
        }

        #endregion
    }

    [BurstCompile]
    public partial struct ClearBuffersJob : IJobEntity
    {
        //public EntityCommandBuffer.ParallelWriter ecb;
        public BufferLookup<BoidInCellBufferData> boidInCellLookup;

        public void Execute([ChunkIndexInQuery] int sortKey, Entity entity)
        {
            //ecb.SetBuffer<BoidInCellBufferData>(sortKey, entity).Clear();
            var buffer = boidInCellLookup[entity];
            buffer.Clear();
        }
    }

    [BurstCompile]
    public partial struct DistributeBoidsInCellsJob : IJobEntity
    {
        [ReadOnly] public NativeArray<Entity> cells;
        [ReadOnly] public ComponentLookup<GridCellInfo> cellLookup;

        [ReadOnly] public BoidBehaviourInfo boidInfo;

        public BufferLookup<BoidInCellBufferData> boidInCellLookup;
        //public EntityCommandBuffer.ParallelWriter ecb;

        public float2 boundsMin;
        public float2 boundsMax;
        public float2 boundsSize;
        public int2 gridSize;

        public void Execute(in LocalTransform transform,
                            ref BoidData boidData,
                            [ChunkIndexInQuery] int sortKey, Entity entity)
        {
            // get the info we'll store
            float2 pos = transform.Position.xy;
            float2 dir = transform.Right().xy;

            // find the cell where to store it
            int cellIndex = GetCell(pos.x, pos.y);

            // finally add it to the buffer
            Entity cellEntity = cells[cellIndex];
            var buffer = boidInCellLookup[cellEntity];
            buffer.Add(new BoidInCellBufferData
            {
                boid = entity,
                pos = pos,
                dir = dir,
            });
            //ecb.AppendToBuffer<BoidInCellBufferData>(sortKey, cellEntity, new BoidInCellBufferData
            //{
            //    boid = entity,
            //    pos = pos,
            //    dir = dir,
            //});

            // update the boid data
            boidData.cellIndex = cellIndex;
        }

        /// <summary>
        /// Returns the cell index
        /// </summary>
        public int GetCell(float x, float y)
        {
            float xNorm = (x - boundsMin.x) / boundsSize.x;
            float yNorm = (y - boundsMin.y) / boundsSize.y;
            int ix = math.clamp(Mathf.FloorToInt(xNorm * gridSize.x), 0, gridSize.x - 1);
            int iy = math.clamp(Mathf.FloorToInt(yNorm * gridSize.y), 0, gridSize.y - 1);

            int index = iy * gridSize.x + ix;

            return index;
        }
    }

}
