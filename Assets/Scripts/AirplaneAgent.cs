using System.Collections;
using UnityEngine;
using Unity.MLAgents;
using Unity.Barracuda;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class AirplaneAgent : Agent
{
    private EndArea end; 
    Rigidbody rigid_plane;
    EnvironmentParameters reset_params;

    public int stepTimeout = 1000;

    private float nextStepTimeout;

    public int NextCheckpointIndex { get; set; }

    public float thrust = 100000f;
    public float pitchSpeed = 100f;
    public float yawSpeed = 100f;
    public float rollSpeed = 100f;
    public float boostMultiplier = 2f;

    private float pitchChange = 0f;
    private float smoothPitchChange = 0f;
    private float maxPitchAngle = 45f;
    private float yawChange = 0f;
    private float smoothYawChange = 0f;
    private float rollChange = 0f;
    private float smoothRollChange = 0f;
    private float maxRollAngle = 45f;
    private bool boost;

    public override void Initialize() {

        // get important components and initialize stepcount if training
        rigid_plane = GetComponent<Rigidbody>();
        end = GetComponentInParent<EndArea>();
        if (end.trainingMode) {
            MaxStep = 10000;
        } else {
            MaxStep = 0;
        }
    }   

    public override void OnEpisodeBegin() {
        // set velocity to 0 at beginning, and reset the agent to either a random position if training, or the initial checkpoint
        rigid_plane.velocity = Vector3.zero;
        rigid_plane.angularVelocity = Vector3.zero;
        end.ResetAgentPosition(agent: this, randomize: end.trainingMode);
        if (end.trainingMode) {
            // reset steps if training
            nextStepTimeout = StepCount + stepTimeout;
        }
        
    }

    // in ML Agents, agent must observe their environment to decide actions to perform
    public override void CollectObservations(VectorSensor sensor) {

        // need to get distance from target / target location
        // need to get velocity
        // both done by sensors (RayCasts)
        sensor.AddObservation(transform.InverseTransformDirection(rigid_plane.velocity));
        sensor.AddObservation(vector_distance());

        Vector3 nextCheckpointForward = end.Checkpoints[NextCheckpointIndex].transform.forward;
        sensor.AddObservation(transform.InverseTransformDirection(nextCheckpointForward));
    }

    

    // defined in ML Agents, whenever the agent decides to perform an action it will call this
    public override void OnActionReceived(ActionBuffers actionBuffers) {
        
        // set plane movements 
        pitchChange = actionBuffers.DiscreteActions[0]; 
        if (pitchChange == 2) pitchChange = -1f; 
        yawChange = actionBuffers.DiscreteActions[1]; 
        if (yawChange == 2) yawChange = -1f; 
        boost = actionBuffers.DiscreteActions[2] == 1;

        Move();

        // if training, reward plane / check the step count
        if (end.trainingMode) {
            
            AddReward(-1f / MaxStep);
            if (StepCount > nextStepTimeout)
            {
                AddReward(-.5f);
                EndEpisode();
            }
        }
        
        // get distance to next checkpoint
        Vector3 checkpoint_dist = vector_distance();
        if (checkpoint_dist.magnitude < 2.0f) {
            GotCheckpoint();
        }
        
        
        

    }

    // controls/calculates the plane movement, called in OnActionReceived
    private void Move() {
        float boostModifier = boostMultiplier;

        // move plane forward
        GetComponent<Rigidbody>().AddForce(transform.forward * thrust * boostModifier, ForceMode.Force);

        // get rotation
        Vector3 curRot = transform.rotation.eulerAngles;

        // calculate roll
        float rollAngle = curRot.z > 180f ? curRot.z - 360f : curRot.z;
        if (yawChange == 0f)
        {
            // yaw change = 0 means not rolling, so turn towards center
            rollChange = -rollAngle / maxRollAngle;
        }
        else
        {
            // if there is yaw change means we turn, so slowly turn against it
            rollChange = -yawChange;
        }

        // ensure that changes are smooth and not sudden
        smoothPitchChange = Mathf.MoveTowards(smoothPitchChange, pitchChange, 2f * Time.fixedDeltaTime);
        smoothYawChange = Mathf.MoveTowards(smoothYawChange, yawChange, 2f * Time.fixedDeltaTime);
        smoothRollChange = Mathf.MoveTowards(smoothRollChange, rollChange, 2f * Time.fixedDeltaTime);

        // calculate and clamp new pitch yaw and roll
        float pitch = curRot.x + smoothPitchChange * Time.fixedDeltaTime * pitchSpeed;
        if (pitch > 180f) pitch -= 360f;
        pitch = Mathf.Clamp(pitch, -maxPitchAngle, maxPitchAngle);

        float yaw = curRot.y + smoothYawChange * Time.fixedDeltaTime * yawSpeed;

        float roll = curRot.z + smoothRollChange * Time.fixedDeltaTime * rollSpeed;
        if (roll > 180f) roll -= 360f;
        roll = Mathf.Clamp(roll, -maxRollAngle, maxRollAngle);

        // now set the rotation
        transform.rotation = Quaternion.Euler(pitch, yaw, roll);
    }

    // gets vector distance to next checkpoint, returns the distance
    private Vector3 vector_distance() {

        Vector3 nextCheckpointDir = end.Checkpoints[NextCheckpointIndex].transform.position - transform.position;
        Vector3 localCheckpointDir = transform.InverseTransformDirection(nextCheckpointDir);
        return localCheckpointDir;
    }   

    // rewards and updates the plane for reaching a checkpoint
    private void GotCheckpoint()
    {
        // move nextCheckpoint onto the new one
        NextCheckpointIndex = (NextCheckpointIndex + 1) % end.Checkpoints.Count;

        // if training mode, add rewards
        if (end.trainingMode)
        {
            // if nextCheckpoint is the initial one / number 0, it must be mean we reached the final checkpoint
            if (NextCheckpointIndex == 0) {
                ReachedEnd();
            }

            // reward the plane, but still keep incrementing steps since we don't end the episode
            AddReward(.5f);
            nextStepTimeout = StepCount + stepTimeout;
        }
    }

    // for when the plane reaches the last checkpoint, reward it handsomely for doing so
    private void ReachedEnd() {
        AddReward(1f);
        EndEpisode();
    }
    
    // if the plane collides into something that is NOT the checkpoint, it will be punished
    private void OnCollisionEnter(Collision collision) {
        
        // reward if training, otherwise end
        if (end.trainingMode) {
            AddReward(-1f);
        }
        EndEpisode();
        
    }

    // this is to check if the plane collided into a checkpoint, if so it is good
    private void OnTriggerEnter(Collider other)
        {
            if (other.transform.CompareTag("checkpoint") &&
                other.gameObject == end.Checkpoints[NextCheckpointIndex])
            {
                GotCheckpoint();
            }
        }

    // just allows us to test in Heuristic only mode
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.DiscreteActions;
        continuousActionsOut[1] = (int)-Input.GetAxis("Horizontal");
        continuousActionsOut[2] = (int)Input.GetAxis("Vertical");
        continuousActionsOut[0] = (int)Input.GetAxis("Yaw");
    }
}