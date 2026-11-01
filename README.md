# COMPTE RENDU TRAVAIL IA - Margot DELAGE et Noam SEBAHOUN
Dernier modèle d'IA = `FirstFlipper_2_0_0`
- A noter que ce modèle fonctionne de manière optimisé pour le Dual. Elle fonctionnera en Hard et en Easy mais risque d'avoir des performances moyenens. Si vous voulez tester le Hard, prévilégiez le modèle `FirstFlipper_1_2_2` et le modèle `FirstFlipper_0_6_2`pour le Easy.

## Le choix des obeservations

Pour les observations, nous avons opté pour des paramètres susceptible de varier d'essais en essais pour laisser l'IA découvrir le reste avec les entrainements (ex: taille du terrain, taille de la raquette,...).
Ainsi, nous avons opté pour ces observations : 

```csharp
// Extrait de la classe --> FlipperAgent.cs
    
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
```

L'idée était que l'IA puisse évaluer comment tirer de façon optimisé comme on pourrait le faire en tant qu'humain.

## Processus d'amélioration
### Le Score Manager
Pour commencer, nous avons créer un script `ScoreManager.cs` afin d'ajouter ou retirer des points en fonction d'une situation.

Ce script s'appuie un Dictionnaire qui lie un ENUM de récompense à une value à virgule flottante.

[ScreenShot du prefab ScoreManager](ReadMeMedia/Screenshot_ScoreManegerPrefab.png)

A travers un fichier excel générer par le `ScoreManager` et tensorBoard, on a pu observer la courbes d'apprentisage de notre IA et surtout les points sur lesquels elle avait le plus de mal afin d'adapter nos rewards (récompenser une réussite mais pénaliser d'avantage un point de difficulté connu).

[Screenshot du excel de résultat d'une IA](ReadMeMedia/Screenshot_ResultExcel.png)

A la suite des entrainement, on a voulu aussi testé comment l'IA se comporte si on est (beaucoup) plus méchant avec elle. Par conséquent, on a créer une variant de notre préfab de Score Manager avec des rewards majoritairement négatives.

Disons que cette variante à réussi à améliorer quelques points mais ensuite l'IA s'est contenter de ne plus jouer afin d'éviter de perdre trop de points.

### L'évolution des observations

Bien évidemment, nous avons pas mis toutes les observations présentes dans [Le choix des obeservations](#le-choix-des-obeservations), nous avons commencé par quelques observation très génériques telles que :

```csharp
sensor.AddObservation(trainingPinball.Flipper(side).WorldToLocalVector(normalizedBallDir)); // Ball position
sensor.AddObservation(trainingPinball.Flipper(side).WorldToLocalVector(trainingPinball.Target.transform.position)); // Target Position
sensor.AddObservation(trainingPinball.Flipper(side).FinalAngle);
```

Puis on a rajouter (un chouille) d'observations pour arrivé au résulat qu'on a actuellement.

Pour ajouter ces observations, nous avons tenté de nous inspiré d'une [vidéo concernant le Mouvement Balistique](https://www.youtube.com/watch?v=m4d9cFufv9I) et avons adapter le concept global à notre cas.

L'intention était de donner de plus en plus d'observations précises et technique a notre IA afin qu'elle puisse prendre la meilleure décision avec un maximum de paramètres pour l'aider dans à faire un choix.