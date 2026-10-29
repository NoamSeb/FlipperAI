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
    // [SerializeField] float distanceTargetThreshold = 1.5f;

    int id = 0;
    int episodeId = 0;
    // private float distanceWithTarget = 0f;


    PlayerController playerController;


    float buttonPressedTimestamp = 0f;
    float ballInContactTimestamp = 0f;
    float ballEnterFlipperTimestamp = 0f;
    float ballEnterOtherFlipperTimestamp = 0f;
    float episodeBeginTimestamp = 0f;

    bool BallInContact => Time.fixedTime <= ballInContactTimestamp + Time.fixedDeltaTime * 3; // Solves jittering at low speed. We allow 3 fixed update of before counting a new collision as an "OnCollisionEnter"
    
    
    [SerializeField] float distanceTargetThreshold = 1.5f;   // décommenté
    private float distanceWithTarget = -1f;   // décommenté, et passé de 0f à -1f
    
    
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
        CalculateDistanceWithTarget();
    }

    public override void OnEpisodeBegin() {
        trainingPinball.BeginEpisode();
        buttonPressedTimestamp = 0f;
        ballInContactTimestamp = 0f;
        ballEnterFlipperTimestamp = 0f;
        ballEnterOtherFlipperTimestamp = 0;
        episodeBeginTimestamp = Time.fixedTime;
        
        distanceWithTarget = -1f;
    }

    public override void CollectObservations(VectorSensor sensor) {
        Vector2 ballDir = (trainingPinball.Ball.transform.position - trainingPinball.transform.position);

        Vector2 normalizedBallDir = ballDir / trainingPinball.Configuration.AveragePinballDimension;
        
        sensor.AddObservation(trainingPinball.Flipper(side).WorldToLocalVector(normalizedBallDir)); // Ball position
        sensor.AddObservation(trainingPinball.Flipper(side).WorldToLocalVector(trainingPinball.Target.transform.position)); // Target Position
        sensor.AddObservation(trainingPinball.Flipper(side).FinalAngle);
        
        Debug.DrawRay(trainingPinball.transform.position, trainingPinball.Flipper(side).WorldToLocalVector(normalizedBallDir)*10, Color.yellow);
    }
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    

    public override void OnActionReceived(ActionBuffers actions) {

        
        bool isPressed = actions.DiscreteActions[0] > 0;

        if (!trainingPinball.IsButtonPressed(side) && isPressed) {
            buttonPressedTimestamp = Time.fixedTime;
        }
        else if(trainingPinball.IsButtonPressed(side) && !isPressed)
        {
            if(buttonPressedTimestamp > ballInContactTimestamp)
                UpdateScore(Recompenses.NoHit);
        }

        trainingPinball.SetButtonState(isPressed, side);
    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        var discreteActionsOut = actionsOut.DiscreteActions;
        bool isPressed = side == Flipper.FlipperSide.Left ? playerController.Player.LeftFlipper.IsPressed() : playerController.Player.RightFlipper.IsPressed();
        discreteActionsOut[0] = isPressed ? 1 : 0;
    }

    void HandleOnEpisodeEnd()
    {
        AddDistanceWithTargetReward();
        EndEpisode();
        episodeId++;
    }



    void HandleOnBallEnterFlipper(FlipperSide side) {
        // Solves jittering at low speed. We allow 3 fixed update of before counting a new collision as an "OnCollisionEnter"
        if (!BallInContact) {
            ballEnterFlipperTimestamp = Time.fixedTime;
            
            ScoreManager.Instance.totalShoot += 1;
        }

        HandleOnBallInContactWithFlipper(side);
    }

    void HandleOnBallInContactWithFlipper(FlipperSide side) {
        ballInContactTimestamp = Time.fixedTime;
    }

    void HandleOnBallExitFlipper(FlipperSide side) {
        // Do not use. May be called multiple times due to jittering
    }

    void HandleOnBallHitTarget()
    {
        ScoreManager.Instance.totalHitTarget += 1;
        UpdateScore(Recompenses.HitTarget); // God of all gods
    }

    void HandleOnBallEnterAnyFlipper(FlipperSide side) {
        if (side != this.side) {
            ballEnterOtherFlipperTimestamp = Time.fixedTime;
        }
    }

    void HandleOnBallEnterBound(BoundTrigger.BoundType type) {

        if(type == BoundTrigger.BoundType.Drain)
           UpdateScore(Recompenses.TouchDrain);
        // if(type == BoundTrigger.BoundType.OtherSide)
        //     UpdateScore(Recompenses.Negative);
        if(type == BoundTrigger.BoundType.ResetZone)
            UpdateScore(Recompenses.TriggerResetZone);
    }

    void UpdateScore(Recompenses recompenseType, float divisor = 1f)
    {
        AddReward(ScoreManager.Instance.RecompenseScore.GetValueOrDefault(recompenseType)/divisor);
        ScoreManager.Instance.OnRecompenses.Invoke(recompenseType, id);
    }
    
    
    
    
    [Header("Reward: close to target")]
    [SerializeField] float nearDistance = 6f;       // <= : grosse récompense (diviseur 1)
    [SerializeField] float midDistance = 13f;       // <= : récompense moyenne (diviseur 2), au-delà : faible (diviseur 3)
    [SerializeField] float retreatMargin = 0.3f;    // de combien la balle doit s'éloigner pour compter comme "retombe" (évite le jitter)

    private float currentDistanceToTarget = -1f;
    private float closestDistanceInApproach = Mathf.Infinity;
    private bool approachRewarded = false;
    
    
    
     void CalculateDistanceWithTarget()
     {
         //Vector2 dist =
         //    trainingPinball.Ball.transform.position - trainingPinball.Target.transform.position;
         //if(dist.magnitude<distanceTargetThreshold)
         //    distanceWithTarget = dist.magnitude;
         //
         //Debug.Log("Distance: " + distanceWithTarget);
         
         
         
         
         
         
         
         //currentDistanceToTarget = Vector2.Distance(
         //    trainingPinball.Ball.transform.position,
         //    trainingPinball.Target.transform.position);
         //
         //if (currentDistanceToTarget < distanceTargetThreshold)
         //    distanceWithTarget = currentDistanceToTarget;
         //
         //if (currentDistanceToTarget <= 6)
         //{
         //    UpdateScore(Recompenses.CloseToTarget, 1);
         //   Debug.Log("Récompense grande: " + ScoreManager.Instance.RecompenseScore.GetValueOrDefault(Recompenses.CloseToTarget)/1);
         //}
         //else if (currentDistanceToTarget > 6 && currentDistanceToTarget <= 13)
         //{
         //    UpdateScore(Recompenses.CloseToTarget, 2); 
         //    Debug.Log("Récompense moyenne: " + ScoreManager.Instance.RecompenseScore.GetValueOrDefault(Recompenses.CloseToTarget)/2);
         //}
         //else
         //{
         //    UpdateScore(Recompenses.CloseToTarget, 3);
         //    Debug.Log("Récompense grande: " + ScoreManager.Instance.RecompenseScore.GetValueOrDefault(Recompenses.CloseToTarget)/3);
         //}
         //
         //Debug.Log("Distance réelle: " + currentDistanceToTarget + " | Utilisée pour la récompense: " + distanceWithTarget);
         
         
         
         
         
         
         
         
         
         
         
         
         currentDistanceToTarget = Vector2.Distance(
             trainingPinball.Ball.transform.position,
             trainingPinball.Target.transform.position);

         if (currentDistanceToTarget < distanceTargetThreshold)
             distanceWithTarget = currentDistanceToTarget;

         if (currentDistanceToTarget < closestDistanceInApproach)
         {
             closestDistanceInApproach = currentDistanceToTarget;
             approachRewarded = false;
         }
         else if (!approachRewarded && currentDistanceToTarget > closestDistanceInApproach + retreatMargin)
         {

             if (closestDistanceInApproach <= nearDistance)
             {
                 UpdateScore(Recompenses.CloseToTarget, 2);
             }
             else if (closestDistanceInApproach <= midDistance)
             {
                 UpdateScore(Recompenses.CloseToTarget, 10);
             }
             else
             {
                 UpdateScore(Recompenses.CloseToTarget, 20);
             }
             
             
             //GiveCloseToTargetReward(closestDistanceInApproach);
             
             
             approachRewarded = true;
             closestDistanceInApproach = currentDistanceToTarget; // nouvelle référence pour la prochaine approche
         }
     }
     
     
     
     
     
     
     
     
    
     void AddDistanceWithTargetReward()
     {
         //float divisor = Mathf.Lerp(distanceTargetThreshold, distanceWithTarget,)
         //UpdateScore(Recompenses.CloseToTarget, divisor);
         
         
         if (distanceWithTarget < 0f) return;

         float t = distanceWithTarget / distanceTargetThreshold;
         float divisor = Mathf.Lerp(1f, 4f, t);

         UpdateScore(Recompenses.CloseToTarget, divisor);
     }
}