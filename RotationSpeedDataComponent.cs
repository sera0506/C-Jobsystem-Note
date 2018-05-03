using UnityEngine;
using UnityEngine.Jobs;
using System;
using Unity.Collections;
using Unity.Jobs;

public class RotationSpeedDataComponent : MonoBehaviour
{
    public GameObject prefab  = null;
    public float radius = 30.0f;
    public int number = 500;
    public NativeArray<float> objSpeed;
    public bool useJobSystem = false;

    [SerializeField] Transform[] targets;
    TransformAccessArray targetsArray;
    JobHandle jobHandle;


    struct RotatorJob :IJobParallelForTransform
    {
        [ReadOnly]
        public NativeArray<float> speeds;
        public float deltaTime;

        void IJobParallelForTransform.Execute(int index, TransformAccess transform)
        {
            transform.localRotation = transform.rotation * Quaternion.AngleAxis(speeds[index] * deltaTime, Vector3.up);
        }
    }

    // Use this for initialization
    void Start ()
    {
        targets = new Transform[number];
        objSpeed = new NativeArray<float>(number, Allocator.Persistent);

        for (int i = 0; i < number; i++)
        {
            targets[i] = Instantiate(prefab, UnityEngine.Random.insideUnitSphere * radius + transform.position, UnityEngine.Random.rotation).transform;
            targets[i].parent = gameObject.transform;
            objSpeed[i] = UnityEngine.Random.Range(30.0f, 60.0f);
        }
        targetsArray = new TransformAccessArray(targets);
    }
	
	// Update is called once per frame
	void Update ()
    {
        if(useJobSystem)
        {
            jobHandle.Complete();

            var jobData = new RotatorJob();
            jobData.speeds = objSpeed;
            jobData.deltaTime = Time.deltaTime; // main thread deltatime to job

            jobHandle = jobData.Schedule(targetsArray);
        }
        else
        {
            for (int i = 0; i < number; i++)
            {
                targets[i].localRotation = targets[i].rotation * Quaternion.AngleAxis(objSpeed[i] * Time.deltaTime, Vector3.up);

            }
        }
    }

    private void OnApplicationQuit()
    {
        //if(useJobSystem)
        {
            jobHandle.Complete();
            objSpeed.Dispose();
            targetsArray.Dispose();
        }
    }
}
