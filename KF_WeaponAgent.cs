using Cysharp.Threading.Tasks;
using HotFix.Kongfu.Framework.Events;
using MiniCSharp.Runtime;
using System.Collections.Generic;
using UnityEngine;

//武器系统：管理局内武器生成、飞行、命中和公共武器标识。
public static class KF_WeaponAgent
{
    //武器标识：局外培养和局内使用共用这一套枚举。
    public enum WeaponTag
    {
        None = 0,
        SFBJ,
        DMSQ,
        BS,
        BWQ,
        SJG,
        GLJGQ,
        BZ,
        TX,
        ZXJ,
        TYNSDT,
        XS,
        CYJ,
        ZQ,
        KJCB,
        FF,
        ZD,
        SZD,
        WDFHL,
        DTZ,
        DSP,
    }

    //当前默认局内武器资源。
    public static string KnifePath = "Prefebs/Weapon/SZD";
    public static string NunchakuPath = "Prefebs/Weapon/CYJ";
    public static string CrossArrowPath = "Prefebs/Weapon/TX";

    //暴露给外部查询的武器实体 id。
    public static int KnifeId;
    public static int NunchakuId;
    public static int CrossArrowId;

    //默认武器配置：表数据缺失时使用这些兜底值。
    private const string KnifeConfigName = "Knife";
    private const string NunchakuConfigName = "Nunchaku";
    private const string CrossArrowConfigName = "CrossArrow";
    private const int KnifeWeaponId = 17;
    private const int NunchakuWeaponId = 2;
    private const int CrossArrowWeaponId = 1;
    private const float KnifeDefaultDamageScale = 4f;
    private const float NunchakuDefaultDamageScale = 2.5f;
    private const float CrossArrowDefaultDamageScale = 1.2f;
    private const float KnifeMoveSpeed = 3.5f;
    private const float NunchakuMoveSpeed = 3f;
    private const float CrossArrowMoveSpeed = 2.5f;
    private const float KnifeDefaultInstantKillChancePercent = 5f;
    //这个距离内造成伤害
    private const float WeaponHitDistance = 0.45f;
    //超过这个距离就取消伤害判定
    private const float WeaponHitReleaseDistance = 0.55f;
    private const float WeaponScale = 0.01f;
    private const float WeaponSpinDegreesPerSecond = 720f;
    private const float MaxBouncePredictionSeconds = 1.2f;
    private const float ScreenEdgePadding = 0.18f;
    //这个取相机的z坐标深度
    private const float ViewportWorldDepth = 10f;
    private const int MaxBattleStartingWeaponCount = 5;
    private const int BSWeaponId = 3;
    private const int BZWeaponId = 8;
    private const float GenericDefaultDamageScale = 1f;
    private const float GenericMoveSpeed = 3f;

    public static bool TryGetWeaponId(WeaponTag tag, out int weaponId)
    {
        weaponId = 0;
        switch (tag)
        {
            case WeaponTag.CYJ: weaponId = 1; return true;
            case WeaponTag.TX: weaponId = 2; return true;
            case WeaponTag.BS: weaponId = 3; return true;
            case WeaponTag.DSP: weaponId = 4; return true;
            case WeaponTag.FF: weaponId = 5; return true;
            case WeaponTag.TYNSDT: weaponId = 6; return true;
            case WeaponTag.ZQ: weaponId = 7; return true;
            case WeaponTag.BZ: weaponId = 8; return true;
            case WeaponTag.SJG: weaponId = 9; return true;
            case WeaponTag.KJCB: weaponId = 10; return true;
            case WeaponTag.DTZ: weaponId = 11; return true;
            case WeaponTag.DMSQ: weaponId = 12; return true;
            case WeaponTag.GLJGQ: weaponId = 13; return true;
            case WeaponTag.XS: weaponId = 14; return true;
            case WeaponTag.SFBJ: weaponId = 15; return true;
            case WeaponTag.ZD: weaponId = 16; return true;
            case WeaponTag.SZD: weaponId = 17; return true;
            case WeaponTag.ZXJ: weaponId = 18; return true;
            case WeaponTag.BWQ: weaponId = 19; return true;
            case WeaponTag.WDFHL: weaponId = 20; return true;
            default: return false;
        }
    }

    public static bool TryGetWeaponTag(int weaponId, out WeaponTag tag)
    {
        tag = WeaponTag.None;
        switch (weaponId)
        {
            case 1: tag = WeaponTag.CYJ; return true;
            case 2: tag = WeaponTag.TX; return true;
            case 3: tag = WeaponTag.BS; return true;
            case 4: tag = WeaponTag.DSP; return true;
            case 5: tag = WeaponTag.FF; return true;
            case 6: tag = WeaponTag.TYNSDT; return true;
            case 7: tag = WeaponTag.ZQ; return true;
            case 8: tag = WeaponTag.BZ; return true;
            case 9: tag = WeaponTag.SJG; return true;
            case 10: tag = WeaponTag.KJCB; return true;
            case 11: tag = WeaponTag.DTZ; return true;
            case 12: tag = WeaponTag.DMSQ; return true;
            case 13: tag = WeaponTag.GLJGQ; return true;
            case 14: tag = WeaponTag.XS; return true;
            case 15: tag = WeaponTag.SFBJ; return true;
            case 16: tag = WeaponTag.ZD; return true;
            case 17: tag = WeaponTag.SZD; return true;
            case 18: tag = WeaponTag.ZXJ; return true;
            case 19: tag = WeaponTag.BWQ; return true;
            case 20: tag = WeaponTag.WDFHL; return true;
            default: return false;
        }
    }

    public static string GetWeaponIconPath(WeaponTag tag)
    {
        switch (tag)
        {
            case WeaponTag.SFBJ: return "Texture/WeaponUI/SFBJ";
            case WeaponTag.DMSQ: return "Texture/WeaponUI/DMSQ";
            case WeaponTag.BS: return "Texture/WeaponUI/BS";
            case WeaponTag.BWQ: return "Texture/WeaponUI/BWQ";
            case WeaponTag.SJG: return "Texture/WeaponUI/SJG";
            case WeaponTag.GLJGQ: return "Texture/WeaponUI/GLJGQ";
            case WeaponTag.BZ: return "Texture/WeaponUI/BZ";
            case WeaponTag.TX: return "Texture/WeaponUI/TX";
            case WeaponTag.ZXJ: return "Texture/WeaponUI/ZXJ";
            case WeaponTag.TYNSDT: return "Texture/WeaponUI/TYNSDT";
            case WeaponTag.XS: return "Texture/WeaponUI/XS";
            case WeaponTag.CYJ: return "Texture/WeaponUI/CYJ";
            case WeaponTag.ZQ: return "Texture/WeaponUI/ZQ";
            case WeaponTag.KJCB: return "Texture/WeaponUI/KJCB";
            case WeaponTag.FF: return "Texture/WeaponUI/FF";
            case WeaponTag.ZD: return "Texture/WeaponUI/ZD";
            case WeaponTag.SZD: return "Texture/WeaponUI/SZD";
            case WeaponTag.WDFHL: return "Texture/WeaponUI/WDFHL";
            case WeaponTag.DTZ: return "Texture/WeaponUI/DTZ";
            case WeaponTag.DSP: return "Texture/WeaponUI/DSP";
            default: return string.Empty;
        }
    }

    public static bool TryGetWeaponRankInfo(WeaponTag tag, out WeaponRankInfo info)
    {
        info = default!;
        return TryGetWeaponId(tag, out var weaponId)
            && KF_ResourceManager1.TryGetWeaponRankInfo(weaponId, out info);
    }

    public static string GetWeaponPrefabPath(WeaponTag tag)
    {
        switch (tag)
        {
            case WeaponTag.SZD: return "Prefebs/Weapon/SZD";
            case WeaponTag.CYJ: return "Prefebs/Weapon/CYJ";
            case WeaponTag.TX: return "Prefebs/Weapon/TX";
            case WeaponTag.BS: return "Prefebs/Weapon/BS";
            case WeaponTag.BZ: return "Prefebs/Weapon/BZ";
            default: return string.Empty;
        }
    }

    //单个局内武器的运行态。
    private sealed class WeaponRuntime
    {
        public readonly string ConfigName;
        public readonly int DefaultWeaponId;
        public readonly float DefaultDamageScale;
        public readonly float DefaultInstantKillChancePercent;
        public readonly bool CanInstantKill;
        public readonly bool AlignRotationToDirection;
        public readonly bool SpinContinuously;
        public readonly float SpinDegreesPerSecond;
        public readonly float MoveSpeed;
        public readonly HashSet<int> TouchingEnemyIds = new HashSet<int>();
        public readonly List<int> TouchingEnemyIdsToRemove = new List<int>(32);
        public readonly List<int> PendingHitEnemyIds = new List<int>(16);
        public readonly List<int> BounceTargetEnemyIds = new List<int>(32);
        public readonly List<Vector3> BounceTargetEnemyPositions = new List<Vector3>(32);
        public readonly List<Vector2> BounceTargetEnemyVelocities = new List<Vector2>(32);
        public int EntityId = -1;
        public int CurrentWeaponId;
        public Vector2 FlyDirection;
        public int LastBounceTargetEnemyId = -1;

        public WeaponRuntime(
            string configName,
            int weaponId,
            float defaultDamageScale,
            float defaultInstantKillChancePercent,
            bool canInstantKill,
            bool alignRotationToDirection,
            bool spinContinuously,
            float spinDegreesPerSecond,
            float moveSpeed)
        {
            ConfigName = configName;
            DefaultWeaponId = weaponId;
            CurrentWeaponId = weaponId;
            DefaultDamageScale = defaultDamageScale;
            DefaultInstantKillChancePercent = defaultInstantKillChancePercent;
            CanInstantKill = canInstantKill;
            AlignRotationToDirection = alignRotationToDirection;
            SpinContinuously = spinContinuously;
            SpinDegreesPerSecond = spinDegreesPerSecond;
            MoveSpeed = moveSpeed;
        }
    }

    //当前默认上阵的三件局内武器。
    private static readonly WeaponRuntime KnifeWeapon = new WeaponRuntime(KnifeConfigName, KnifeWeaponId, KnifeDefaultDamageScale, KnifeDefaultInstantKillChancePercent, true, true, false, 0f, KnifeMoveSpeed);
    private static readonly WeaponRuntime NunchakuWeapon = new WeaponRuntime(NunchakuConfigName, NunchakuWeaponId, NunchakuDefaultDamageScale, 0f, false, false, true, WeaponSpinDegreesPerSecond, NunchakuMoveSpeed);
    private static readonly WeaponRuntime CrossArrowWeapon = new WeaponRuntime(CrossArrowConfigName, CrossArrowWeaponId, CrossArrowDefaultDamageScale, 0f, false, false, true, WeaponSpinDegreesPerSecond, CrossArrowMoveSpeed);
    private static readonly WeaponRuntime FourthWeapon = new WeaponRuntime("BS", BSWeaponId, GenericDefaultDamageScale, 0f, false, true, false, 0f, GenericMoveSpeed);
    private static readonly WeaponRuntime FifthWeapon = new WeaponRuntime("BZ", BZWeaponId, GenericDefaultDamageScale, 0f, false, true, false, 0f, GenericMoveSpeed);

    //FlyDirection 为零时表示武器原地停止。
    private static bool HasLastScreenBounds;
    private static ScreenBounds LastScreenBounds;

    private struct ScreenBounds
    {
        public float MinX;
        public float MaxX;
        public float MinY;
        public float MaxY;

        public ScreenBounds(float minX, float maxX, float minY, float maxY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }

        public bool IsValid()
        {
            return MaxX - MinX > 0.01f && MaxY - MinY > 0.01f;
        }

        public bool Contains(Vector3 position, float padding)
        {
            return position.x >= MinX - padding
                && position.x <= MaxX + padding
                && position.y >= MinY - padding
                && position.y <= MaxY + padding;
        }
    }

    public static void Reset()
    {
        //进入战斗和退出战斗都调用这里
        ResetWeapon(KnifeWeapon);
        ResetWeapon(NunchakuWeapon);
        ResetWeapon(CrossArrowWeapon);
        ResetWeapon(FourthWeapon);
        ResetWeapon(FifthWeapon);
        SyncPublicWeaponIds();
        HasLastScreenBounds = false;
        LastScreenBounds = default;
    }

    public static void Tick()
    {
        //每帧驱动所有已生成武器。
        TickWeapon(KnifeWeapon);
        TickWeapon(NunchakuWeapon);
        TickWeapon(CrossArrowWeapon);
        TickWeapon(FourthWeapon);
        TickWeapon(FifthWeapon);
        SyncPublicWeaponIds();
    }

    //默认武器生成入口。
    public static async UniTask TrySpawnKnifeAsync()
    {
        await TrySpawnWeaponAsync(KnifeWeapon, KnifePath);
        SyncPublicWeaponIds();
    }

    public static async UniTask TrySpawnNunchakuAsync()
    {
        await TrySpawnWeaponAsync(NunchakuWeapon, NunchakuPath);
        SyncPublicWeaponIds();
    }

    public static async UniTask TrySpawnCrossArrowAsync()
    {
        await TrySpawnWeaponAsync(CrossArrowWeapon, CrossArrowPath);
        SyncPublicWeaponIds();
    }

    public static async UniTask SpawnSelectedStartingWeaponsAsync()
    {
        var weaponTags = GetBattleStartingWeaponTags();
        for (int i = 0; i < weaponTags.Length; i++)
        {
            if (!TryGetRuntimeByIndex(i, out var runtime)) continue;

            var tag = weaponTags[i];
            if (TryGetWeaponId(tag, out var weaponId))
            {
                runtime.CurrentWeaponId = weaponId;
            }
            else
            {
                runtime.CurrentWeaponId = runtime.DefaultWeaponId;
            }

            var path = GetWeaponPrefabPath(tag);
            if (string.IsNullOrEmpty(path)) continue;

            await TrySpawnWeaponAsync(runtime, path);
        }

        SyncPublicWeaponIds();
    }

    private static void ResetWeapon(WeaponRuntime weapon)
    {
        if (weapon.EntityId > 0 && ScriptRuntime.IsEntityAlive(weapon.EntityId))
        {
            ScriptRuntime.DespawnEntity(weapon.EntityId);
        }

        weapon.EntityId = -1;
        weapon.CurrentWeaponId = weapon.DefaultWeaponId;
        weapon.FlyDirection = Vector2.zero;
        weapon.TouchingEnemyIds.Clear();
        weapon.PendingHitEnemyIds.Clear();
        weapon.TouchingEnemyIdsToRemove.Clear();
        weapon.BounceTargetEnemyIds.Clear();
        weapon.BounceTargetEnemyPositions.Clear();
        weapon.BounceTargetEnemyVelocities.Clear();
        weapon.LastBounceTargetEnemyId = -1;
    }

    private static void TickWeapon(WeaponRuntime weapon)
    {
        //局内武器主循环：旋转、寻敌、移动、命中。
        if (weapon.EntityId <= 0) return;
        if (!ScriptRuntime.IsEntityAlive(weapon.EntityId)) return;

        var dt = ScriptRuntime.GetDeltaTime();
        ApplyContinuousRotation(weapon, dt);
        if (!ScriptRuntime.GetEntityPosition(weapon.EntityId, out var weaponPos)) return;

        if (!TryGetScreenBounds(out var screenBounds))
        {
            StopWeapon(weapon);
            return;
        }

        //屏幕中没有敌人时，武器原地停止。
        if (!HasEnemyOnScreen(screenBounds))
        {
            StopWeapon(weapon);
            return;
        }

        //缩小飞行区域
        var moveBounds = ShrinkBounds(screenBounds, ScreenEdgePadding);
        //从停止状态重新发现敌人时，按弹墙逻辑锁定敌人预判位置开始飞行。
        if (weapon.FlyDirection.sqrMagnitude <= 0.0001f)
        {
            SetBounceDirectionByEnemyTarget(weapon, weaponPos, moveBounds);
        }

        MoveWeaponInsideBounds(weapon, weaponPos, moveBounds, dt);
    }

    private static async UniTask TrySpawnWeaponAsync(WeaponRuntime weapon, string path)
    {
        if (weapon.EntityId > 0 && ScriptRuntime.IsEntityAlive(weapon.EntityId)) return;
        if (KF_PlayerAgent.PlayerId <= 0) return;
        if (!ScriptRuntime.GetEntityPosition(KF_PlayerAgent.PlayerId, out var playerPos)) return;

        weapon.EntityId = await ScriptRuntime.TrySpawnEntity(path, playerPos);
        if (weapon.EntityId <= 0) return;

        ScriptRuntime.SetEntityLocalScaleXYZ(weapon.EntityId, WeaponScale, WeaponScale, WeaponScale);
        StopWeapon(weapon);
    }

    private static void MoveWeaponInsideBounds(WeaponRuntime weapon, Vector3 weaponPos, ScreenBounds moveBounds, float dt)
    {
        var nextPos = weaponPos + new Vector3(weapon.FlyDirection.x, weapon.FlyDirection.y, 0f) * GetWeaponMoveSpeed(weapon) * dt;

        var hitBorder = false;
        if (nextPos.x < moveBounds.MinX)
        {
            nextPos.x = moveBounds.MinX;
            hitBorder = true;
        }
        else if (nextPos.x > moveBounds.MaxX)
        {
            nextPos.x = moveBounds.MaxX;
            hitBorder = true;
        }

        if (nextPos.y < moveBounds.MinY)
        {
            nextPos.y = moveBounds.MinY;
            hitBorder = true;
        }
        else if (nextPos.y > moveBounds.MaxY)
        {
            nextPos.y = moveBounds.MaxY;
            hitBorder = true;
        }

        ScriptRuntime.SetEntityPosition(weapon.EntityId, nextPos);
        if (hitBorder)
        {
            //碰到屏幕边框后，随机锁定一个存活敌人的方向继续飞。
            SetBounceDirectionByEnemyTarget(weapon, nextPos, moveBounds);
        }

        ApplyDirectionRotation(weapon);
        TryHitEnemies(weapon, nextPos);
    }

    private static void StopWeapon(WeaponRuntime weapon)
    {
        //清空方向并重置贴近命中状态。
        weapon.FlyDirection = Vector2.zero;
        weapon.TouchingEnemyIds.Clear();
        weapon.LastBounceTargetEnemyId = -1;
    }

    private static void SetRandomInwardDirection(WeaponRuntime weapon, Vector3 weaponPos, ScreenBounds bounds)
    {
        //随机屏幕内一点作为目标
        var targetBounds = ShrinkBounds(bounds, ScreenEdgePadding);
        var targetX = ScriptRuntime.RandomRange(targetBounds.MinX, targetBounds.MaxX);
        var targetY = ScriptRuntime.RandomRange(targetBounds.MinY, targetBounds.MaxY);
        var dir = new Vector2(targetX - weaponPos.x, targetY - weaponPos.y);
        if (dir.sqrMagnitude <= 0.0001f)
        {
            SetRandomScreenDirection(weapon);
            return;
        }

        weapon.FlyDirection = dir.normalized;
    }

    private static void SetRandomScreenDirection(WeaponRuntime weapon)
    {
        //找不到有效敌人方向时的兜底随机方向。
        var angle = ScriptRuntime.RandomRange(0f, 360f) * Mathf.Deg2Rad;
        weapon.FlyDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
    }

    private static void SetBounceDirectionByEnemyTarget(WeaponRuntime weapon, Vector3 weaponPos, ScreenBounds moveBounds)
    {
        //弹墙后优先朝敌人预判位置飞行。
        if (!TryChooseBounceTargetEnemy(weapon, out var targetEnemyId, out var targetPos, out var targetVelocity))
        {
            SetRandomInwardDirection(weapon, weaponPos, moveBounds);
            return;
        }

        var predictedPos = PredictBounceTargetPosition(weapon, weaponPos, targetPos, targetVelocity, moveBounds);
        var dir = new Vector2(predictedPos.x - weaponPos.x, predictedPos.y - weaponPos.y);
        if (dir.sqrMagnitude <= 0.0001f)
        {
            weapon.LastBounceTargetEnemyId = targetEnemyId;
            SetRandomInwardDirection(weapon, weaponPos, moveBounds);
            return;
        }

        weapon.LastBounceTargetEnemyId = targetEnemyId;
        weapon.FlyDirection = dir.normalized;
    }

    private static bool TryChooseBounceTargetEnemy(WeaponRuntime weapon, out int targetEnemyId, out Vector3 targetPos, out Vector2 targetVelocity)
    {
        //从当前存活敌人里选择一个弹射目标。
        targetEnemyId = -1;
        targetPos = default;
        targetVelocity = Vector2.zero;
        weapon.BounceTargetEnemyIds.Clear();
        weapon.BounceTargetEnemyPositions.Clear();
        weapon.BounceTargetEnemyVelocities.Clear();

        foreach (var kv in KF_EnemyAgent.EnemyIds)
        {
            var enemyId = kv.Key;
            if (enemyId <= 0) continue;
            if (!ScriptRuntime.IsEntityAlive(enemyId)) continue;
            if (!ScriptRuntime.DictTryGet(KF_EnemyAgent.EnemyIds, enemyId, out var enemyInfo)) continue;
            if (enemyInfo.HP <= 0f) continue;
            if (enemyInfo.CurActionState == ActionState._Death) continue;
            if (!ScriptRuntime.GetEntityPosition(enemyId, out var enemyPos)) continue;

            weapon.BounceTargetEnemyIds.Add(enemyId);
            weapon.BounceTargetEnemyPositions.Add(enemyPos);
            weapon.BounceTargetEnemyVelocities.Add(GetEnemyPredictVelocity(enemyId, enemyInfo, enemyPos));
        }

        var targetCount = weapon.BounceTargetEnemyIds.Count;
        if (targetCount <= 0) return false;

        var targetIndex = GetBounceTargetIndex(weapon, targetCount);
        targetEnemyId = weapon.BounceTargetEnemyIds[targetIndex];
        targetPos = weapon.BounceTargetEnemyPositions[targetIndex];
        targetVelocity = weapon.BounceTargetEnemyVelocities[targetIndex];
        return true;
    }

    private static Vector2 GetEnemyPredictVelocity(int enemyId, EnemyCurInfo enemyInfo, Vector3 enemyPos)
    {
        if (ScriptRuntime.GetEntityRigidbody2DVelocity(enemyId, out var velocity))
        {
            return velocity;
        }

        if (enemyInfo.CurActionState != ActionState._Move) return Vector2.zero;
        if (enemyInfo.IsTouchingPlayer) return Vector2.zero;
        if (enemyInfo.MoveSpeed <= 0f) return Vector2.zero;
        if (KF_PlayerAgent.PlayerId <= 0) return Vector2.zero;
        if (!ScriptRuntime.GetEntityPosition(KF_PlayerAgent.PlayerId, out var playerPos)) return Vector2.zero;

        var dir = new Vector2(playerPos.x - enemyPos.x, playerPos.y - enemyPos.y);
        if (dir.sqrMagnitude <= 0.0001f) return Vector2.zero;
        return dir.normalized * enemyInfo.MoveSpeed;
    }

    private static Vector3 PredictBounceTargetPosition(WeaponRuntime weapon, Vector3 weaponPos, Vector3 targetPos, Vector2 targetVelocity, ScreenBounds moveBounds)
    {
        //按武器速度和敌人速度估算交汇点。
        var leadSeconds = CalculateLeadSeconds(weapon, weaponPos, targetPos, targetVelocity);
        var predictedPos = targetPos + new Vector3(targetVelocity.x, targetVelocity.y, 0f) * leadSeconds;
        predictedPos.x = Mathf.Clamp(predictedPos.x, moveBounds.MinX, moveBounds.MaxX);
        predictedPos.y = Mathf.Clamp(predictedPos.y, moveBounds.MinY, moveBounds.MaxY);
        return predictedPos;
    }

    private static float CalculateLeadSeconds(WeaponRuntime weapon, Vector3 weaponPos, Vector3 targetPos, Vector2 targetVelocity)
    {
        var relativePos = new Vector2(targetPos.x - weaponPos.x, targetPos.y - weaponPos.y);
        var distanceSqr = relativePos.sqrMagnitude;
        if (distanceSqr <= 0.0001f) return 0f;
        var weaponMoveSpeed = Mathf.Max(0.01f, GetWeaponMoveSpeed(weapon));
        if (targetVelocity.sqrMagnitude <= 0.0001f)
        {
            return Mathf.Clamp(Mathf.Sqrt(distanceSqr) / weaponMoveSpeed, 0f, MaxBouncePredictionSeconds);
        }

        var a = targetVelocity.sqrMagnitude - weaponMoveSpeed * weaponMoveSpeed;
        var b = 2f * Vector2.Dot(relativePos, targetVelocity);
        var c = distanceSqr;
        var leadSeconds = Mathf.Sqrt(distanceSqr) / weaponMoveSpeed;

        if (Mathf.Abs(a) <= 0.0001f)
        {
            if (Mathf.Abs(b) > 0.0001f)
            {
                var linearTime = -c / b;
                if (linearTime > 0f) leadSeconds = linearTime;
            }
        }
        else
        {
            var discriminant = b * b - 4f * a * c;
            if (discriminant >= 0f)
            {
                var sqrtDiscriminant = Mathf.Sqrt(discriminant);
                var t1 = (-b - sqrtDiscriminant) / (2f * a);
                var t2 = (-b + sqrtDiscriminant) / (2f * a);
                if (t1 > 0f && t2 > 0f)
                {
                    leadSeconds = Mathf.Min(t1, t2);
                }
                else if (t1 > 0f)
                {
                    leadSeconds = t1;
                }
                else if (t2 > 0f)
                {
                    leadSeconds = t2;
                }
            }
        }

        return Mathf.Clamp(leadSeconds, 0f, MaxBouncePredictionSeconds);
    }

    private static int GetBounceTargetIndex(WeaponRuntime weapon, int targetCount)
    {
        if (targetCount <= 1) return 0;

        var lastTargetIndex = -1;
        for (int i = 0; i < targetCount; i++)
        {
            if (weapon.BounceTargetEnemyIds[i] == weapon.LastBounceTargetEnemyId)
            {
                lastTargetIndex = i;
                break;
            }
        }

        if (lastTargetIndex < 0) return ScriptRuntime.RandomRange(0, targetCount);

        //存活敌人大于等于 2 时，排除上一次弹射锁定的敌人。
        var randomIndex = ScriptRuntime.RandomRange(0, targetCount - 1);
        if (randomIndex >= lastTargetIndex)
        {
            randomIndex++;
        }

        return randomIndex;
    }

    private static void ApplyDirectionRotation(WeaponRuntime weapon)
    {
        //刀的视觉方向跟随飞行方向，双节棍保持自身持续旋转。
        if (!ShouldAlignRotationToDirection(weapon)) return;
        if (weapon.FlyDirection.sqrMagnitude <= 0.0001f) return;

        var angle = Mathf.Atan2(weapon.FlyDirection.y, weapon.FlyDirection.x) * Mathf.Rad2Deg;
        ScriptRuntime.SetEntityRotationEulerXYZ(weapon.EntityId, 0f, 0f, angle);
    }

    private static void ApplyContinuousRotation(WeaponRuntime weapon, float dt)
    {
        //双节棍从生成开始一直自转，碰到边框时不按飞行方向重设角度。
        if (!ShouldSpinContinuously(weapon)) return;

        var spinDegreesPerSecond = GetSpinDegreesPerSecond(weapon);
        if (spinDegreesPerSecond <= 0f) return;

        ScriptRuntime.RotateEntity(weapon.EntityId, new Vector3(0f, 0f, spinDegreesPerSecond * dt), Space.Self);
    }

    private static void TryHitEnemies(WeaponRuntime weapon, Vector3 weaponPos)
    {
        //根据武器和敌人的距离判断命中。
        CleanupTouchingEnemies(weapon, weaponPos);

        weapon.PendingHitEnemyIds.Clear();
        var hitDistanceSqr = WeaponHitDistance * WeaponHitDistance;
        foreach (var kv in KF_EnemyAgent.EnemyIds)
        {
            var enemyId = kv.Key;
            if (enemyId <= 0) continue;
            if (!ScriptRuntime.IsEntityAlive(enemyId)) continue;
            if (!ScriptRuntime.GetEntityPosition(enemyId, out var enemyPos)) continue;

            var distanceSqr = (enemyPos - weaponPos).sqrMagnitude;
            if (distanceSqr > hitDistanceSqr) continue;
            if (!weapon.TouchingEnemyIds.Add(enemyId)) continue;

            weapon.PendingHitEnemyIds.Add(enemyId);
        }

        var damage = GetWeaponDamage(weapon);

        for (int i = 0; i < weapon.PendingHitEnemyIds.Count; i++)
        {
            //武器伤害不吃玩家暴击/吸血等升级效果。
            var enemyId = weapon.PendingHitEnemyIds[i];
            if (!ScriptRuntime.DictTryGet(KF_EnemyAgent.EnemyIds, enemyId, out var enemyInfo)) continue;
            var finalDamage = GetWeaponHitDamage(weapon, enemyInfo, damage);
            KF_EnemyAgent.TakeFixedDamage(enemyId, finalDamage, true);
        }
    }

    private static float GetWeaponHitDamage(WeaponRuntime weapon, EnemyCurInfo enemyInfo, float damage)
    {
        if (enemyInfo == null) return damage;
        if (TryRollInstantKill(weapon))
        {
            //5% 概率必杀，直接造成敌人当前血量的伤害。
            return enemyInfo.HP > 0f ? enemyInfo.HP : damage;
        }

        if (TryRollWeaponCritical(weapon))
        {
            return damage * GetWeaponCriticalDamageMultiplier(weapon);
        }

        return damage;
    }

    private static bool TryRollInstantKill(WeaponRuntime weapon)
    {
        if (!CanWeaponInstantKill(weapon)) return false;

        var chance = GetWeaponInstantKillChancePercent(weapon);
        if (chance <= 0f) return false;
        return ScriptRuntime.RandomRange(0f, 100f) < chance;
    }

    private static float GetWeaponDamage(WeaponRuntime weapon)
    {
        //武器基础倍率来自 0022 atk/atkAdd；等级效果、星级、固定词条额外作为固定伤害叠加。
        var damageScale = KF_ResourceManager1.GetWeaponDamageScale(weapon.CurrentWeaponId, GetDefaultDamageScale(weapon));
        damageScale *= KF_CharacterUIManager.GetWeaponPerkDamageMultiplierByWeaponId(weapon.CurrentWeaponId);
        var baseDamage = KF_PlayerAgent.CurInfo.Damage * damageScale;
        var flatDamageBonus = KF_CharacterUIManager.GetWeaponFlatDamageBonusByWeaponId(weapon.CurrentWeaponId);
        return Mathf.Max(0f, baseDamage + flatDamageBonus);
    }

    private static float GetWeaponInstantKillChancePercent(WeaponRuntime weapon)
    {
        return KF_ResourceManager1.GetWeaponInstantKillChancePercent(weapon.CurrentWeaponId, GetDefaultInstantKillChancePercent(weapon));
    }

    private static float GetWeaponMoveSpeed(WeaponRuntime weapon)
    {
        var moveSpeed = KF_ResourceManager1.GetWeaponMoveSpeed(weapon.CurrentWeaponId, GetDefaultMoveSpeed(weapon));
        return moveSpeed * KF_CharacterUIManager.GetWeaponPerkMoveSpeedMultiplierByWeaponId(weapon.CurrentWeaponId);
    }

    private static bool TryRollWeaponCritical(WeaponRuntime weapon)
    {
        var criticalRate = KF_CharacterUIManager.GetWeaponPerkCriticalRatePercentByWeaponId(weapon.CurrentWeaponId);
        if (criticalRate <= 0f) return false;

        return ScriptRuntime.RandomRange(0f, 100f) < Mathf.Clamp(criticalRate, 0f, 100f);
    }

    private static float GetWeaponCriticalDamageMultiplier(WeaponRuntime weapon)
    {
        return KF_CharacterUIManager.GetWeaponPerkCriticalDamageMultiplierByWeaponId(weapon.CurrentWeaponId);
    }

    private static WeaponInfo? GetWeaponInfo(string configName)
    {
        // 武器编组查询统一走资源层，避免业务侧自己遍历缓存。
        return KF_ResourceManager.TryGetWeaponFormationInfo(configName, out var info) ? info : null;
    }

    public static string[] GetBattleStartingWeaponPaths()
    {
        var tags = GetBattleStartingWeaponTags();
        var paths = new string[tags.Length];
        for (int i = 0; i < tags.Length; i++)
        {
            paths[i] = GetWeaponPrefabPath(tags[i]);
        }

        return paths;
    }

    private static WeaponTag[] GetBattleStartingWeaponTags()
    {
        var selected = KF_CharacterUIManager.GetCurrentSelectedWeaponSlots();
        var result = new WeaponTag[MaxBattleStartingWeaponCount];
        var count = 0;

        // 只带入当前已装备且确实能在战斗中生成 prefab 的武器。
        for (int i = 0; i < selected.Length && count < result.Length; i++)
        {
            var tag = selected[i];
            if (tag == WeaponTag.None) continue;
            if (string.IsNullOrEmpty(GetWeaponPrefabPath(tag))) continue;

            result[count++] = tag;
        }

        var trimmed = new WeaponTag[count];
        for (int i = 0; i < count; i++)
        {
            trimmed[i] = result[i];
        }

        return trimmed;
    }

    private static bool TryGetRuntimeByIndex(int index, out WeaponRuntime runtime)
    {
        runtime = null;
        switch (index)
        {
            case 0:
                runtime = KnifeWeapon;
                return true;
            case 1:
                runtime = NunchakuWeapon;
                return true;
            case 2:
                runtime = CrossArrowWeapon;
                return true;
            case 3:
                runtime = FourthWeapon;
                return true;
            case 4:
                runtime = FifthWeapon;
                return true;
            default:
                return false;
        }
    }

    private static bool CanWeaponInstantKill(WeaponRuntime weapon)
    {
        return weapon.CurrentWeaponId == KnifeWeaponId;
    }

    private static bool ShouldAlignRotationToDirection(WeaponRuntime weapon)
    {
        return weapon.CurrentWeaponId != NunchakuWeaponId
            && weapon.CurrentWeaponId != CrossArrowWeaponId
            && weapon.CurrentWeaponId != BSWeaponId
            && weapon.CurrentWeaponId != BZWeaponId;
    }

    private static bool ShouldSpinContinuously(WeaponRuntime weapon)
    {
        return weapon.CurrentWeaponId == NunchakuWeaponId
            || weapon.CurrentWeaponId == CrossArrowWeaponId
            || weapon.CurrentWeaponId == BSWeaponId
            || weapon.CurrentWeaponId == BZWeaponId;
    }

    private static float GetSpinDegreesPerSecond(WeaponRuntime weapon)
    {
        return ShouldSpinContinuously(weapon) ? WeaponSpinDegreesPerSecond : 0f;
    }

    private static float GetDefaultDamageScale(WeaponRuntime weapon)
    {
        switch (weapon.CurrentWeaponId)
        {
            case KnifeWeaponId: return KnifeDefaultDamageScale;
            case NunchakuWeaponId: return NunchakuDefaultDamageScale;
            case CrossArrowWeaponId: return CrossArrowDefaultDamageScale;
            default: return GenericDefaultDamageScale;
        }
    }

    private static float GetDefaultInstantKillChancePercent(WeaponRuntime weapon)
    {
        return weapon.CurrentWeaponId == KnifeWeaponId ? KnifeDefaultInstantKillChancePercent : 0f;
    }

    private static float GetDefaultMoveSpeed(WeaponRuntime weapon)
    {
        switch (weapon.CurrentWeaponId)
        {
            case KnifeWeaponId: return KnifeMoveSpeed;
            case NunchakuWeaponId: return NunchakuMoveSpeed;
            case CrossArrowWeaponId: return CrossArrowMoveSpeed;
            default: return GenericMoveSpeed;
        }
    }

    private static void CleanupTouchingEnemies(WeaponRuntime weapon, Vector3 weaponPos)
    {
        //敌人离开释放距离后，下次再碰到武器可以重新受伤。
        weapon.TouchingEnemyIdsToRemove.Clear();
        var releaseDistanceSqr = WeaponHitReleaseDistance * WeaponHitReleaseDistance;
        foreach (var enemyId in weapon.TouchingEnemyIds)
        {
            if (enemyId <= 0
                || !ScriptRuntime.IsEntityAlive(enemyId)
                || !ScriptRuntime.DictTryGet(KF_EnemyAgent.EnemyIds, enemyId, out var _)
                || !ScriptRuntime.GetEntityPosition(enemyId, out var enemyPos)
                || (enemyPos - weaponPos).sqrMagnitude > releaseDistanceSqr)
            {
                weapon.TouchingEnemyIdsToRemove.Add(enemyId);
            }
        }

        for (int i = 0; i < weapon.TouchingEnemyIdsToRemove.Count; i++)
        {
            weapon.TouchingEnemyIds.Remove(weapon.TouchingEnemyIdsToRemove[i]);
        }
    }

    private static void SyncPublicWeaponIds()
    {
        KnifeId = KnifeWeapon.EntityId;
        NunchakuId = NunchakuWeapon.EntityId;
        CrossArrowId = CrossArrowWeapon.EntityId;
    }

    private static bool HasEnemyOnScreen(ScreenBounds bounds)
    {
        //只要屏幕范围内存在活着的敌人，武器就保持飞行。
        if (KF_EnemyAgent.EnemyIds == null || ScriptRuntime.DictCount(KF_EnemyAgent.EnemyIds) <= 0) return false;

        foreach (var kv in KF_EnemyAgent.EnemyIds)
        {
            var enemyId = kv.Key;
            if (enemyId <= 0) continue;
            if (!ScriptRuntime.IsEntityAlive(enemyId)) continue;
            if (!ScriptRuntime.GetEntityPosition(enemyId, out var enemyPos)) continue;
            if (bounds.Contains(enemyPos, 0.05f)) return true;
        }

        return false;
    }

    private static bool TryGetScreenBounds(out ScreenBounds bounds)
    {
        //相机边界获取失败时，优先复用上一帧有效边界。
        if (TryGetCameraScreenBounds(out bounds))
        {
            LastScreenBounds = bounds;
            HasLastScreenBounds = true;
            return true;
        }

        if (HasLastScreenBounds)
        {
            bounds = LastScreenBounds;
            return true;
        }

        return TryGetFallbackScreenBounds(out bounds);
    }

    private static bool TryGetCameraScreenBounds(out ScreenBounds bounds)
    {
        //通过 ViewportToWorldPoint 转换当前屏幕四角到世界坐标。
        bounds = default;
        //左下角
        if (!ScriptRuntime.ViewportToWorldPoint(0f, 0f, ViewportWorldDepth, out var bottomLeft)) return false;
        //右上角
        if (!ScriptRuntime.ViewportToWorldPoint(1f, 1f, ViewportWorldDepth, out var topRight)) return false;

        var minX = Mathf.Min(bottomLeft.x, topRight.x);
        var maxX = Mathf.Max(bottomLeft.x, topRight.x);
        var minY = Mathf.Min(bottomLeft.y, topRight.y);
        var maxY = Mathf.Max(bottomLeft.y, topRight.y);
        bounds = new ScreenBounds(minX, maxX, minY, maxY);
        return bounds.IsValid();
    }

    private static bool TryGetFallbackScreenBounds(out ScreenBounds bounds)
    {
        //没有相机边界时，用敌人出生点范围估算一个战斗区域。
        bounds = new ScreenBounds(-3.6f, 3.6f, -2.2f, 4f);
        // 资源层拿不到出生点范围时，继续使用这组保守默认边界。
        if (!KF_ResourceManager.TryGetEnemySpawnBounds(out var minX, out var maxX, out var minY, out var maxY))
        {
            return bounds.IsValid();
        }

        bounds = new ScreenBounds(minX, maxX, minY, maxY);
        return bounds.IsValid();
    }

    private static ScreenBounds ShrinkBounds(ScreenBounds bounds, float padding)
    {
        //【安全缩边】避免边界过小时被 padding 反向压缩。
        if (padding <= 0f) return bounds;
        if (bounds.MaxX - bounds.MinX <= padding * 2f) return bounds;
        if (bounds.MaxY - bounds.MinY <= padding * 2f) return bounds;

        return new ScreenBounds(
            bounds.MinX + padding,
            bounds.MaxX - padding,
            bounds.MinY + padding,
            bounds.MaxY - padding);
    }

}
