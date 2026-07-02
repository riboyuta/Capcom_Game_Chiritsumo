public enum GameEffectKey
{
    None = 0,

    // Player
    PlayerWalkDust,
    PlayerDashSpeedLine,
    PlayerDashBackRing,
    PlayerJumpGround,
    PlayerWallSlideDust,
    PlayerWallJumpImpact,
    PlayerSpawn,
    PlayerDeathBurst,
    PlayerTurnDust,
    PlayerStopDust,
    PlayerLandImpact,
    PlayerJumpAir,

    // Gimmick
    GimmickDashRecoverGet,
    GimmickDashRecoverGlow,
    GimmickKeyGlow,
    GimmickDoorGlow,
    GimmickDoorOpenClose,
    GimmickSpringUse,
    GimmickRailMoveSpark,
    GimmickKeyGet,

    // Enemy
    EnemyHandAura,  
    EnemyShadowAura,    
    EnemyShadowSpawn,   
    EnemySonarAura,     //突進敵のオーラ
    EnemySonarPulseRing,     //突進敵のソナーリング
    EnemySonarWallHit,      //突進敵の壁ヒット
    EnemySonarChargePrepare,        //突進敵のチャージ溜め
    EnemySonarChargeDebris  
}