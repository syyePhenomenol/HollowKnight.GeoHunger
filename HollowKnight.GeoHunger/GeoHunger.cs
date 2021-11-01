using System;
using System.Collections;
using System.Collections.Generic;
using Modding;
using UnityEngine;
using UnityEngine.SceneManagement;
using UObject = UnityEngine.Object;

namespace GeoHunger
{
    public class GeoHunger : Mod, IGlobalSettings<GlobalSettings>
    {
        internal static GeoHunger Instance;

        private readonly string _version = "1.0.0";

        public override string GetVersion() => _version;

        public override int LoadPriority() => 2047;

        public static GlobalSettings GS { get; set; } = new GlobalSettings();

        public void OnLoadGlobal(GlobalSettings s) => GS = s;

        public GlobalSettings OnSaveGlobal() => GS;

        internal static Coroutine depleteGeo = null;
        internal static Coroutine starvePlayer = null;
        internal static bool depleteGeoRunning = false;
        internal static bool starvePlayerRunning = false;

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            Log("Initializing");

            Instance = this;

            On.GameManager.StartNewGame += GameManager_StartNewGame;
            On.GameManager.LoadGame += GameManager_LoadGame;

            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;

            Log("Initialized");
        }

        public void GameManager_StartNewGame(On.GameManager.orig_StartNewGame orig, GameManager self, bool permadeathMode, bool bossRushMode)
        {
            orig(self, permadeathMode, bossRushMode);

            StartDepleting();
        }

        public void GameManager_LoadGame(On.GameManager.orig_LoadGame orig, GameManager self, int saveSlot, Action<bool> callback)
        {
            orig(self, saveSlot, callback);

            StartDepleting();
        }

        public void SceneManager_activeSceneChanged(Scene from, Scene to)
        {
            Log(to.name);

            if (to.name == "Quit_To_Menu")
            {
                StopDepleting();
                StopStarving();
            }
        }

        public void StartDepleting()
        {
            if (!depleteGeoRunning)
            {
                depleteGeo = GameManager.instance.StartCoroutine(DepleteGeo());
                depleteGeoRunning = true;
            }
        }

        public void StopDepleting()
        {
            if (depleteGeoRunning)
            {
                GameManager.instance.StopCoroutine(depleteGeo);
                depleteGeoRunning = false;
            }
        }

        public IEnumerator DepleteGeo()
        {
            while (true)
            {
                if (HeroController.instance == null
                || PlayerData.instance == null
                || !GameManager.instance.IsGameplayScene()
                || GameManager.instance.IsGamePaused())
                {
                    yield return null;
                    continue;
                }

                if (PlayerData.instance.geo > 0)
                {
                    StopStarving();

                    yield return new WaitForSeconds(GS.GeoDepleteTimeSeconds);

                    if (CanTakeDamage())
                    {
                        PlayerData.instance.TakeGeo(1);
                        HeroController.instance.geoCounter.NewSceneRefresh();
                        HeroController.instance.geoCounter.addTextMesh.text = "";
                    }
                }
                else
                {
                    yield return null;
                }

                if (PlayerData.instance.geo <= 0 && CanTakeDamage())
                {
                    StartStarving();
                }
                else
                {
                    StopStarving();
                }
            }
        }

        public bool CanTakeDamage()
        {
            return HeroController.instance.damageMode != GlobalEnums.DamageMode.NO_DAMAGE
                && HeroController.instance.transitionState == GlobalEnums.HeroTransitionState.WAITING_TO_TRANSITION
                && !HeroController.instance.cState.invulnerable
                && !HeroController.instance.cState.recoiling
                && !HeroController.instance.playerData.GetBool("isInvincible")
                && !HeroController.instance.cState.dead
                && !HeroController.instance.cState.hazardDeath
                && !BossSceneController.IsTransitioning
                && !HeroController.instance.cState.nearBench
                && !HeroController.instance.cState.shadowDashing
                && !HeroController.instance.cState.spellQuake
                && !HeroController.instance.cState.isPaused
                && !HeroController.instance.controlReqlinquished;
        }

        public void StartStarving()
        {
            if (!starvePlayerRunning)
            {
                starvePlayer = GameManager.instance.StartCoroutine(StarvePlayer());
                starvePlayerRunning = true;
            }
        }

        public void StopStarving()
        {
            if (starvePlayerRunning)
            {
                GameManager.instance.StopCoroutine(starvePlayer);
                starvePlayerRunning = false;
            }
        }

        public IEnumerator StarvePlayer()
        {
            while (true)
            {
                if (HeroController.instance == null
                || PlayerData.instance == null
                || !GameManager.instance.IsGameplayScene()
                || GameManager.instance.IsGamePaused())
                {
                    yield return null;
                    continue;
                }

                yield return new WaitForSeconds(GS.TakeDamageTimeSeconds);

                HeroController.instance.TakeDamage(null, GlobalEnums.CollisionSide.other, 1, 1);
            }
        }
    }

    public class GlobalSettings
    {
        public float GeoDepleteTimeSeconds = 1.5f;
        public float TakeDamageTimeSeconds = 5.0f;
    }
}