using Unity.Collections;
using UnityEngine;

public class SpawnSphere1 : MonoBehaviour {
    public GameObject prefab;
    public float radius;
    public int number;

    Transform[] targets = null;
    NativeArray<float> velocities;

    // Use this for initialization
    void Start ()
    {
        targets = new Transform[number];

        velocities = new NativeArray<float>(targets.Length, Allocator.Temp);

        for (int i = 0; i < number; i++)
        {
            targets[i] = Instantiate(prefab, Random.insideUnitSphere * radius + transform.position, Random.rotation).transform;
            targets[i].parent = gameObject.transform;
            velocities[i] = -1;
        }

        Debug.Log("target number : " + targets.Length);
    }
	
	// Update is called once per frame
	void Update ()
    {
        var results = new NativeArray<RaycastHit>(targets.Length, Allocator.Temp);
        for (int i = 0; i < targets.Length; i++)
        {
            RaycastHit hit;
            Physics.Raycast(targets[i].localPosition, Vector3.down, out hit, 200);
            results[i] = hit;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if ((velocities[i] < 0) && (results[i].distance < 0.5f))
            {
                velocities[i] = 2;
            }
            velocities[i] -= 0.098f;
        }
        results.Dispose();

        for (int i = 0; i < targets.Length; i++)
        {
            targets[i].localPosition += Vector3.up * velocities[i];
        }
    }

    private void OnApplicationQuit()
    {
        velocities.Dispose();
        
    }
}
