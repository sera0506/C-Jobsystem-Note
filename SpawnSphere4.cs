using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Jobs;


/// <summary>
/// https://docs.unity3d.com/2018.1/Documentation/ScriptReference/RaycastCommand.html
/// </summary>
public class SpawnSphere4 : MonoBehaviour
{
    public GameObject prefab;
    public float radius;
    public int number;

    [SerializeField] Transform[] targets;

    NativeArray<float> velocities;
    NativeArray<RaycastCommand> commands;
    NativeArray<RaycastHit> results;
    TransformAccessArray transformArray;

    JobHandle handle;

    struct UpdatePosition : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<RaycastHit> raycastResults;
        public NativeArray<float> objVelocitys;

        void IJobParallelFor.Execute(int index)
        {
            if (objVelocitys[index] < 0 &&
                raycastResults[index].distance < 0.5f)
            {
                objVelocitys[index] = 2;
            }
            objVelocitys[index] -= 0.098f;
        }
    }

    //  on the c# job , you can't just access all the other existing component classes that we have
    struct ApplyPosition : IJobParallelForTransform
    {
        public NativeArray<float> objVelocitys;

        void IJobParallelForTransform.Execute(int index, TransformAccess transform)
        {
            transform.localPosition += Vector3.up * objVelocitys[index];
        }
    }

    private void OnEnable()
    {
        
    }
    // Use this for initialization
    void Start ()
    {
        targets = new Transform[number];

        velocities = new NativeArray<float>(targets.Length, Allocator.Temp);
        commands = new NativeArray<RaycastCommand>(targets.Length, Allocator.Persistent);
        results = new NativeArray<RaycastHit>(targets.Length, Allocator.Persistent);

        for (int i = 0; i < number; i++)
        {
            targets[i] = Instantiate(prefab, Random.insideUnitSphere * radius + transform.position, Random.rotation).transform;
            targets[i].parent = gameObject.transform;
            velocities[i] = -1;
        }
        transformArray = new TransformAccessArray(targets);
        Debug.Log("target number : " + targets.Length);
    }
	
	// Update is called once per frame
	void Update ()
    {
        handle.Complete();

        for (int i = 0; i < targets.Length; i++)
        {
            var targetPosition = targets[i].position;
            var direction = Vector3.down;
            var command = new RaycastCommand(targetPosition, direction);
            commands[i] = command;
        }

        //  移動的command 設定
        UpdatePosition updatePositionJob = new UpdatePosition()
        {
            raycastResults = results,
            objVelocitys = velocities
        };

        ApplyPosition applyPosition = new ApplyPosition()
        {
            objVelocitys = velocities
        };

        var raycastJobHandle = RaycastCommand.ScheduleBatch(commands, results, 20);
        var updatePositionHandle = updatePositionJob.Schedule(transformArray.length, 20, raycastJobHandle);
        handle = applyPosition.Schedule(transformArray, updatePositionHandle);
    }

    private void OnApplicationQuit()
    {
        handle.Complete();
        velocities.Dispose();
        commands.Dispose();
        results.Dispose();
        transformArray.Dispose();
    }
}
