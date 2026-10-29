using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using static Flipper;
using System.Collections.Generic;

public class FlipperAgent : Agent {

    public static int NextAgentId = 0;

    [SerializeField] TrainingPinballArea trainingPinball;
    [SerializeField] Flipper.FlipperSide side = Flipper.FlipperSide.Left;

    int id = 0;
    int episodeId = 0;


    PlayerController playerController;


    float buttonPressedTimestamp = 0f;
    float ballInContactTimestamp = 0f;
    float ballEnterFlipperTimestamp = 0f;
    float ballEnterOtherFlipperTimestamp = 0f;
    float episodeBeginTimestamp = 0f;

    bool BallInContact => Time.fixedTime <= ballInContactTimestamp + Time.fixedDeltaTime * 3;

    float previousDistanceToTarget = -1f;
    bool shotCountedForPress = false;
    int targetHitsInEpisode = 0;
    int drainsInEpisode = 0;

    void Awake() {
        playerController = new PlayerController();
        playerController.Enable();
        id = NextAgentId++;
    }

    void Start() {
        trainingPinball.Flipper(side).onBallEnter += HandleOnBallEnterFlipper;
        trainingPinball.Flipper(side).onBallStay += HandleOnBallInContactWithFlipper;
        trainingPinball.Flipper(side).onBallExit += HandleOnBallExitFlipper;
        if (trainingPinball.LeftFlipper) {
            trainingPinball.LeftFlipper.onBallEnter += HandleOnBallEnterAnyFlipper;
        }
        if (trainingPinball.RightFlipper) {
            trainingPinball.RightFlipper.onBallEnter += HandleOnBallEnterAnyFlipper;
        }

        trainingPinball.onBallEnterBound += HandleOnBallEnterBound;
        trainingPinball.onBallHitTarget += HandleOnBallHitTarget;
        
        trainingPinball.onEpisodeEnd += HandleOnEpisodeEnd;
    }

    private void OnDestroy() {
        trainingPinball.Flipper(side).onBallEnter -= HandleOnBallEnterFlipper;
        trainingPinball.Flipper(side).onBallStay -= HandleOnBallInContactWithFlipper;
        trainingPinball.Flipper(side).onBallExit -= HandleOnBallExitFlipper;
        if (trainingPinball.LeftFlipper) {
            trainingPinball.LeftFlipper.onBallEnter -= HandleOnBallEnterAnyFlipper;
        }
        if (trainingPinball.RightFlipper) {
            trainingPinball.RightFlipper.onBallEnter -= HandleOnBallEnterAnyFlipper;
        }

        trainingPinball.onBallEnterBound -= HandleOnBallEnterBound;
        trainingPinball.onBallHitTarget -= HandleOnBallHitTarget;
        trainingPinball.onEpisodeEnd -= HandleOnEpisodeEnd;
    }

    private void FixedUpdate() {
        UpdateCloseToTargetReward();
    }

    public override void OnEpisodeBegin() {
        trainingPinball.BeginEpisode();
        buttonPressedTimestamp = 0f;
        ballInContactTimestamp = 0f;
        ballEnterFlipperTimestamp = 0f;
        ballEnterOtherFlipperTimestamp = 0;
        episodeBeginTimestamp = Time.fixedTime;

        previousDistanceToTarget = -1f;
        shotCountedForPress = false;
        targetHitsInEpisode = 0;
        drainsInEpisode = 0;
    }

    public override void CollectObservations(VectorSensor sensor) {
        Vector2 ballDir = (trainingPinball.Ball.transform.position - trainingPinball.transform.position);

        Vector2 normalizedBallDir = ballDir / trainingPinball.Configuration.AveragePinballDimension;
        
        Vector2 targetDir = (trainingPinball.Target.transform.position - trainingPinball.transform.position);
        Vector2 normalizedTargetDir = targetDir / trainingPinball.Configuration.AveragePinballDimension;

        Vector2 normalizedBallVelocity = trainingPinball.Ball.linearVelocity / trainingPinball.Configuration.AveragePinballDimension;

        sensor.AddObservation(trainingPinball.Flipper(side).WorldToLocalVector(normalizedBallDir));
        sensor.AddObservation(trainingPinball.Flipper(side).WorldToLocalVector(normalizedTargetDir));
        sensor.AddObservation(trainingPinball.Flipper(side).FinalAngle);
        sensor.AddObservation(trainingPinball.Flipper(side).WorldToLocalVector(normalizedBallVelocity));
        sensor.AddObservation(trainingPinball.IsButtonPressed(side));
        sensor.AddObservation(BallInContact);
        
        Debug.DrawRay(trainingPinball.transform.position, trainingPinball.Flipper(side).WorldToLocalVector(normalizedBallDir)*10, Color.yellow);
    }

    public override void OnActionReceived(ActionBuffers actions) {

        
        bool isPressed = actions.DiscreteActions[0] > 0;

        if (!trainingPinball.IsButtonPressed(side) && isPressed) {
            buttonPressedTimestamp = Time.fixedTime;
            shotCountedForPress = false;
        }
        else if(trainingPinball.IsButtonPressed(side) && !isPressed)
        {
            if(buttonPressedTimestamp > ballInContactTimestamp)
                UpdateScore(Recompenses.NoHit);
        }

        trainingPinball.SetButtonState(isPressed, side);
        CountShot();
    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        var discreteActionsOut = actionsOut.DiscreteActions;
        bool isPressed = side == Flipper.FlipperSide.Left ? playerController.Player.LeftFlipper.IsPressed() : playerController.Player.RightFlipper.IsPressed();
        discreteActionsOut[0] = isPressed ? 1 : 0;
    }

    void HandleOnEpisodeEnd()
    {
        EndEpisode();
        episodeId++;
    }



    void HandleOnBallEnterFlipper(FlipperSide side) {
        if (!BallInContact) {
            ballEnterFlipperTimestamp = Time.fixedTime;
        }

        HandleOnBallInContactWithFlipper(side);
    }

    void HandleOnBallInContactWithFlipper(FlipperSide side) {
        ballInContactTimestamp = Time.fixedTime;
        CountShot();
    }

    void HandleOnBallExitFlipper(FlipperSide side) {
        // Do not use. May be called multiple times due to jittering
    }

    void HandleOnBallHitTarget()
    {
        ScoreManager.Instance.totalHitTarget += 1;
        targetHitsInEpisode++;
        previousDistanceToTarget = -1f;
        UpdateScore(Recompenses.HitTarget); // God of all gods
    }

    void HandleOnBallEnterAnyFlipper(FlipperSide side) {
        if (side != this.side) {
            ballEnterOtherFlipperTimestamp = Time.fixedTime;
        }
    }

    void HandleOnBallEnterBound(BoundTrigger.BoundType type) {
        previousDistanceToTarget = -1f;

        if (type == BoundTrigger.BoundType.Drain) {
            drainsInEpisode++;
            UpdateScore(Recompenses.TouchDrain);
        }
        // if(type == BoundTrigger.BoundType.OtherSide)
        //     UpdateScore(Recompenses.Negative);
        if(type == BoundTrigger.BoundType.ResetZone)
            UpdateScore(Recompenses.TriggerResetZone);
    }

    void UpdateScore(Recompenses recompenseType, float factor = 1f)
    {
        float reward = ScoreManager.Instance.RecompenseScore.GetValueOrDefault(recompenseType) * factor;
        AddReward(reward);
        ScoreManager.Instance.OnRecompenses.Invoke(recompenseType, id, reward);
    }
    
    void UpdateCloseToTargetReward() {
        float distance = Vector2.Distance(trainingPinball.Ball.transform.position, trainingPinball.Target.transform.position);

        if (previousDistanceToTarget >= 0f) {
            float distanceGain = (previousDistanceToTarget - distance) / trainingPinball.Configuration.AveragePinballDimension.magnitude;
            UpdateScore(Recompenses.CloseToTarget, distanceGain);
        }

        previousDistanceToTarget = distance;
    }

    void CountShot() {
        if (shotCountedForPress || !trainingPinball.IsButtonPressed(side) || !BallInContact) { return; }

        shotCountedForPress = true;
        ScoreManager.Instance.totalShoot += 1;
    }
}