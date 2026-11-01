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
    [SerializeField] float impactPredictionHorizon = 1.5f;
    [SerializeField] float closeToTargetThreshold = 20f;

    int id = 0;
    int episodeId = 0;


    PlayerController playerController;


    float buttonPressedTimestamp = 0f;
    float ballInContactTimestamp = 0f;
    float ballEnterFlipperTimestamp = 0f;
    float ballEnterOtherFlipperTimestamp = 0f;
    float episodeBeginTimestamp = 0f;

    bool BallInContact => Time.fixedTime <= ballInContactTimestamp + Time.fixedDeltaTime * 3;

    Flipper ControlledFlipper => trainingPinball.Flipper(side);
    float NormalizationLength => trainingPinball.Configuration.AveragePinballDimension.magnitude;
    Vector2 BallPosition => trainingPinball.Ball.transform.position;
    float DistanceToTarget => Vector2.Distance(BallPosition, trainingPinball.Target.transform.position);

    float closestDistanceToTarget = -1f;
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
        UpdateClosestDistanceToTarget();
    }

    public override void OnEpisodeBegin() {
        trainingPinball.BeginEpisode();
        buttonPressedTimestamp = 0f;
        ballInContactTimestamp = 0f;
        ballEnterFlipperTimestamp = 0f;
        ballEnterOtherFlipperTimestamp = 0;
        episodeBeginTimestamp = Time.fixedTime;

        closestDistanceToTarget = -1f;
        shotCountedForPress = false;
        targetHitsInEpisode = 0;
        drainsInEpisode = 0;
    }
    
    public override void CollectObservations(VectorSensor sensor) {
        Vector2 flipperPosition = ControlledFlipper.transform.position;
        Vector2 ballPosition = BallPosition;
        Vector2 targetPosition = trainingPinball.Target.transform.position;
        Vector2 ballToTarget = targetPosition - ballPosition;

        sensor.AddObservation(ToFlipperFrame(ballPosition - flipperPosition) / NormalizationLength); // Ball position (2)
        sensor.AddObservation(ToFlipperFrame(trainingPinball.Ball.linearVelocity) / NormalizationLength); // Ball velocity (2)
        sensor.AddObservation(ToFlipperFrame(targetPosition - flipperPosition) / NormalizationLength); // Target position (2)
        sensor.AddObservation(ToFlipperFrame(ballToTarget.normalized)); // Aim direction from the ball (2)
        sensor.AddObservation(ballToTarget.magnitude / NormalizationLength); // Ball to target distance (1)

        sensor.AddObservation(ControlledFlipper.CurrentAngleOffset / trainingPinball.Configuration.FlipperAngleOffsetMax); // (1)
        sensor.AddObservation(trainingPinball.IsButtonPressed(side)); // (1)
        sensor.AddObservation(BallInContact); // (1)

        PredictImpact(out bool impactPredicted, out float timeToImpact, out float impactPosition);
        sensor.AddObservation(impactPredicted); // (1)
        sensor.AddObservation(timeToImpact / impactPredictionHorizon); // (1)
        sensor.AddObservation(impactPosition / NormalizationLength); // Predicted ball position at iimpact (les loups et tout) (1)
        
        // Total = 15

        Debug.DrawLine(flipperPosition, ballPosition, Color.yellow);
        if (impactPredicted) {
            Debug.DrawLine(ballPosition, flipperPosition + FromFlipperFrame(new Vector2(impactPosition, 0f)), Color.red);
        }
    }

    public override void OnActionReceived(ActionBuffers actions) {
        bool isPressed = actions.DiscreteActions[0] > 0;
        bool wasPressed = trainingPinball.IsButtonPressed(side);

        if (!wasPressed && isPressed) {
            buttonPressedTimestamp = Time.fixedTime;
            shotCountedForPress = false;
        }
        else if (wasPressed && !isPressed && buttonPressedTimestamp > ballInContactTimestamp) {
            UpdateScore(Recompenses.NoHit);
        }
        
        if (isPressed && !BallInContact) {
            UpdateScore(Recompenses.ButtonPressedWithouBall, Time.fixedDeltaTime);
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
        AddCloseToTargetReward();
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
        //ScoreManager.Instance.totalHitTarget += 1;
        targetHitsInEpisode++;
        closestDistanceToTarget = 0f;
        UpdateScore(Recompenses.HitTarget); // God of all gods
    }

    void HandleOnBallEnterAnyFlipper(FlipperSide side) {
        if (side != this.side) {
            ballEnterOtherFlipperTimestamp = Time.fixedTime;
        }
    }

    void HandleOnBallEnterBound(BoundTrigger.BoundType type) {
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

    void UpdateClosestDistanceToTarget() {
        if (closestDistanceToTarget < 0f) { return; }

        closestDistanceToTarget = Mathf.Min(closestDistanceToTarget, DistanceToTarget);
    }
    
    void AddCloseToTargetReward() {
        if (closestDistanceToTarget < 0f || closestDistanceToTarget >= closeToTargetThreshold) { return; }

        UpdateScore(Recompenses.CloseToTarget, 1f - closestDistanceToTarget / closeToTargetThreshold);
    }

    void CountShot() {
        if (shotCountedForPress || !trainingPinball.IsButtonPressed(side) || !BallInContact) { return; }

        shotCountedForPress = true;
        //ScoreManager.Instance.totalShoot += 1;

        if (closestDistanceToTarget < 0f) {
            closestDistanceToTarget = DistanceToTarget;
        }
    }
    
    void PredictImpact(out bool impactPredicted, out float timeToImpact, out float impactPosition) {
        Rigidbody2D ball = trainingPinball.Ball;
        Vector2 position = ToFlipperFrame(BallPosition - (Vector2)ControlledFlipper.transform.position);
        Vector2 velocity = ToFlipperFrame(ball.linearVelocity);
        Vector2 gravity = ToFlipperFrame(Physics2D.gravity * ball.gravityScale);
        float height = Mathf.Max(position.y - trainingPinball.Configuration.GetBallScale() * 0.5f, 0f);
        
        float time = (velocity.y + Mathf.Sqrt(velocity.y * velocity.y - 2f * gravity.y * height)) / -gravity.y;

        impactPredicted = position.y > 0f && time <= impactPredictionHorizon;
        timeToImpact = impactPredicted ? time : impactPredictionHorizon;
        impactPosition = impactPredicted ? position.x + velocity.x * time + 0.5f * gravity.x * time * time : 0f;
    }

    Vector2 ToFlipperFrame(Vector2 worldVector) {
        Vector2 local = ControlledFlipper.WorldToLocalVector(worldVector);
        local.x *= ControlledFlipper.SideFactor;
        return local;
    }

    Vector2 FromFlipperFrame(Vector2 localVector) {
        localVector.x *= ControlledFlipper.SideFactor;
        return Quaternion.Euler(0, 0, ControlledFlipper.InitialAngle) * localVector;
    }
}
