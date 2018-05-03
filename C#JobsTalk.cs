
===== Performance

1. good control over memory layout.

=====Data Oriented Design overview

* CPUs read data in cachelines (32 or 64 bytes)
*Reading a cachelines from RAM (~100 - 300 cycles latency)


class PlayerData					// 40 bytes
{
	public string 		name;		// 8 bytes
	public int[] 		sameValue;	// 8 bytes

	public Quaternion 	rotation;	// 16 bytes
	public float3 		position;	// 12 bytes
}
PlayerData[] players;

PlayerData FindClosest(float3 position)
{
	for(int i = 0; i != entities.latency; i++)
	{
		if(math.distance(entities[i].position, position) < 0.5)
			return entities[i];
	}
}

//	Avoid pointer chasing in loops

struct PlayerData
{
	public string		name;		// 8 bytes
	public int[]		someValues;	// 8 bytes

	public Quaternion 	rotation;	// 16 bytes
	public float3 		position;	// 12 bytes
}
PlayerData[] players;

int FindClosest(float3 position)
{
	for(int i = 0; i != entities.latency; i++)
	{
		if(math.distance(entities[i].position, position) < 0.5)
			return i;
	}
}


//	hot cold split, use every byte loaded into a cacheline

struct PlayerData					// 32 bytes
{
	public string		name;		// 8 bytes
	public int[]		someValues;	// 8 bytes

	public Quaternion 	rotation;	// 16 bytes
}
PlayerData[] players;
float3[] playerPositions;			// 12 bytes per element

int FindClosest(float3 position)
{
	for(int i = 0; i != entities.latency; i++)
	{
		if(math.distance(playerPositions[i].position, position) < 0.5)
			return i;
	}
}


*	Data oriented Design
*	No Virtual functions
	-	Access random memory location for the class vtable
	-	Indirect function call, compiler can't inline, slower to invoke

*	memory tightly packed, linear Access
*	no GC allocations, explicit memory management
*	Being upfront and clear about what data we Access

If we want to get great performance we need to let go of OOP patterns.
The new Entity component system is all about making that easy.





===== Componet & Systems

Component contain data
Systems have behaviour

Rotator example
Hooking up game objects in scenes & existing components






===== Iteration - Component Group & InjectTuples

class MySystem : ComponentSystem
{
	[InjectTuples]
	ComponentArray<Transform>		transforms;

	[InjectTuples]
	ComponentDataArray<RotatorData>	rotators;
}

or use ComponentGroup API directly

//	Specify required set of components
var group = m_EntityManager.GetComponentGroup(typeof(RotatorData), typeof(Transform));

//	extract arrays
ComponentDataArray<RotatorData> rotators = group.GetComponentDataArray<RotatorData>();
ComponentArray<Transform> transforms = group.GetComponentArray<Transforms>();

for(int i = 0; i < rotators.Length; i++)
{
	transforms[i].Rotate(0, rotators[i], 0);
}





===== Create & Destroying Entities - Fastpath for pure IComponentData Entities

//	Instantiate entites one by one
// 	(4 components, 320 bytes of data per component)
//	100k instances = 26ms
class Spawner : ScriptBehaviour
{
	public GameObject minionPrefab;

	[InjectDependency]
	EntityManager m_EntityManager;

	public void SpawnMinion()
	{
		Entity entity = m_EntityManager.Instantiate(prefab);
	
		var position = new MinionPosition(Random.position, Random.rotation);
		m_EntityManager.SetComponent(entity, position);
		// like :e = entity.getcomponent<Transform>();
		//e.postion = position;
	}
}




//	Instantiate entites in batch
// 	(4 components, 320 bytes of data per component)
//	100k instances = 9ms
//	memcpy of the same amount = 7ms
class Spawner : ScriptBehaviour
{
	public GameObject minionPrefab;

	[InjectDependency]
	EntityManager m_EntityManager;

	public void SpawnMinion()
	{
		var enetities = new NativeArray<Entity>(count, Allocator.Temp);
		m_EntityManager.Instantiate(prefab, entities);
	
		for(int i = 0; i < count; i ++)
		{
			var position = new MinionPosition(Random.position, Random.rotation);
			m_EntityManager.SetComponent(enetities[i], position);
		}
	}
}





//	One by one
//	Destroy 100k entities = 7.6ms
Entity entity;
m_EntityManager.DestroyEntity(entity);

//	Destroy in batch
//	Destroy 100k entities = 0.9ms
NativeArray<Entity> entityArray = ...;
m_EntityManager.DestroyEntity(entity);

//	Add Component
m_EntityManager.AddComponent(entity, new MyComponentData(...));

//	Remove Component
m_EntityManager.RemoveComponent<MyComponentData>(entity);

//	Change Component
m_EntityManager.SetComponent(entity, new MyComponentData(...));








=====	Memory layout

*	Entities with the same set of components share data layout in chunks
*	Iteration is linear
*	Control over component layout
*	Memory layout can be changed at runtime
*	ComponentData is always kept packed / defragmentation








=====	Native Containers

//	Full control over memory.
//	All containers are allocated with Allocator labels
// 	Giving you explicit control over the memory lifetime.
//	(Temp, TempJob, Persistent)
var array = new NativeArray<int>(100, Allocator.TempJob);

//	Usage is exactly the same as a normal array
array[0] = 5;

//	Dispose must be manually called, since it is not garbage collected
//	Unity provides automatic, convenient leak detection in the editor
//	in case you forget to call Dispose
array.Dispose();	//	It's manual memory management , you;re resposible for
// disposing the native arrays


//	Commonly useful container types are supported, each with an API mirroring
//	familiar C# API's like Array, List, Dictionary etc
struct NativeArray<T>
struct NativeList<T>
struct NativeSlice<T>
struct NativeHashmap<Key, Value>
struct NativeMultiHashmap<Key, Value>


//	NOTE: Native containers can only hold structs or value types.
//	NOTE: Container types are defined in C# so custom
//	containers can be created using unsafe code in C#







=====	IJob

//	C# jobs are defined as struct implementing one of various job interfaces
struct CopyFloatsJob : IJob
{
	//	Jobs declare all data that will be accessed in the job
	//	For guaranteeing job safey, it is also require to declare if data is only read.
	[ReadOnly]
	public NativeArray<float> src;

	//	By default containers are assumed to be read & write
	public NativeArray<float> dst;

	//	The code actually running on the job
	public void Execute()
	{
		for (int i = 0; i < src.Length; i++)
			dst[i] = src[i];
	}
}


// ↓ On the main thread
var job = new CopyFloatJob()
{
	src = new NativeArray<float>(500, Allocator.Temp);
	dst = new NativeArray<float>(500, Allocator.Temp);
}

JobHandle jobHandle = job.Schedule();
src[0] = 5;	// might got error, because job not ready, 
// you write at the same time . (race condition)


// Need results ready immediately!
jobHandle.Complete();	// job has completed at that time
src[0] = 5;				// now  you can write to this data 

//	At this point the src array contents has been copied to [dst]








=====	IJobParallelFor

//	IJobParallelFor automatically subdivides the foreach in chunk
//	Executing multiple batches of paralell for each iterations.
// 	It's like writing the for loop, but your for loop can run in parallel on different threads
struct CopyFloatsJobFor : IJobParallelFor
{
	[ReadOnly]
	public NativeArray<float> src;

	public NativeArray<float> dst;

	public void Execute(int index)
	{
		dst[index] = src[index];
	}
}


//	Fill job data
var job = new CopyFloatsJobFor()
{
	src = new NativeArray<float>(500, Allocator.Temp);
	dst = new NativeArray<float>(500, Allocator.Temp);
}

//	how many iterations shoud we perform
//	in this case we want to run 500 iterations
//	because that's how much data we have
//	The amount of foreach & batch size can be controlled at schedule time
job.Schedule(500,100);






=====	Dependencies
struct CopyFloatsJobFor : IJobParallelFor
{
	[ReadOnly]
	public NativeArray<float> src;

	public NativeArray<float> dst;

	public void Execute(int index)
	{
		dst[index] = src[index];
	}
}


//	Schedule job a copying from src -> dst
var src = new NativeArray<float>(500, Allocator.Temp);
var	dst = new NativeArray<float>(500, Allocator.Temp);
var jobA = new CopyFloatsJobFor(){src = src , dst = dst;}
var jobAHandle = jobA.Schedule(src.Length, 100);


//	Schedule job  b copying from dst -> finalDst
var finalDst = new NativeArray<float>(500, Allocator.Temp);
var jobB = new CopyFloatsJobFor(){src = dst, dst = finalDst};
//	Sceduling the job with dependency jobAHandle.
//	The job will only begin executing on a worker thread
//	Once jobA Completes.
var jobBHandle = jobB.Schedule(src.Length, 100, jobAHandle);

//	(reprioritize job & dependencies & potential execute job on main thread right away)
//	both jobs will be completed, first jobA then jobB that will always complete in this order。
//	because you put that dependency there. 
jobBHandle.Complete();

//	jobs are asynchronous and independent of frames
// can also run heavy jobs that span multiple frames.