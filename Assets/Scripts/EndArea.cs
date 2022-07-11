using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EndArea : MonoBehaviour
{
    public CinemachineSmoothPath path;

    public GameObject checkpointPrefab;

    public GameObject finish;

    public bool trainingMode;

    public List<AirplaneAgent> AirplaneAgents { get; private set; }
    public List<GameObject> Checkpoints { get; private set; }

    // do this when script wakes up 
    private void Awake()
    {
        // get all agents (if using multiple)
        AirplaneAgents = transform.GetComponentsInChildren<AirplaneAgent>().ToList();
    }

    // similar to Awake, but only do this if script is enabled
    private void Start()
    {
        
        // create checkpoints based on prefabs
        Checkpoints = new List<GameObject>();
        int numCheckpoints = (int)path.MaxUnit(CinemachinePathBase.PositionUnits.PathUnits);
        for (int i = 0; i <= numCheckpoints; i++)
        {
            GameObject checkpoint;
            if (i == numCheckpoints) checkpoint = Instantiate<GameObject>(finish);
            else checkpoint = Instantiate<GameObject>(checkpointPrefab);

            // set relationship, rotation, and location
            checkpoint.transform.SetParent(path.transform);
            checkpoint.transform.localPosition = path.m_Waypoints[i].position;
            checkpoint.transform.rotation = path.EvaluateOrientationAtUnit(i, CinemachinePathBase.PositionUnits.PathUnits);

            // add to list
            Checkpoints.Add(checkpoint);
        }
    }

    // resets the agent either to the beginning of the course or to a random checkpoint if training
    public void ResetAgentPosition(AirplaneAgent agent, bool randomize = false)
    {
        if (randomize)
        {
            // pick random checkpoint
            agent.NextCheckpointIndex = Random.Range(0, Checkpoints.Count-1);
        } else {
            // pick start
            agent.NextCheckpointIndex = 1;
        }

        // using nextCheckpoint, set the agent location to the previous checkpoint
        int previousCheckpointIndex = agent.NextCheckpointIndex - 1;
        if (previousCheckpointIndex == -1) previousCheckpointIndex = Checkpoints.Count - 2;

        float startPosition = path.FromPathNativeUnits(previousCheckpointIndex, CinemachinePathBase.PositionUnits.PathUnits);

        // use racePath position to 3D world position
        Vector3 basePosition = path.EvaluatePosition(startPosition);

        // get orientation/rotation
        Quaternion orientation = path.EvaluateOrientation(startPosition);

        // if multiple planes, spread them out horizontally
        Vector3 positionOffset = Vector3.right * (AirplaneAgents.IndexOf(agent) - AirplaneAgents.Count / 2f) * Random.Range(9f, 10f);

        // set airplane in the checkpoint accordingly
        agent.transform.position = basePosition + orientation * positionOffset;
        agent.transform.rotation = orientation;
    }
}

