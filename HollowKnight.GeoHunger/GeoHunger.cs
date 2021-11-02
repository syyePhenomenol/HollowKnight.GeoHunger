using System;
using System.Collections;
using System.Collections.Generic;
using Modding;
using UnityEngine;
using UnityEngine.SceneManagement;
using UObject = UnityEngine.Object;

namespace GeoHunger
{
    public class GeoHunger : Mod, IGlobalSettings<GlobalSettings>, IMenuMod, ITogglableMod
    {
        internal static GeoHunger Instance;

        private readonly string _version = "1.2.0";

        public override string GetVersion() => _version;

        public override int LoadPriority() => 2047;

        public static GlobalSettings GS { get; set; } = new GlobalSettings();

        public bool ToggleButtonInsideMenu => false;

        public void OnLoadGlobal(GlobalSettings s) => GS = s;

        public GlobalSettings OnSaveGlobal() => GS;

        internal static Coroutine depleteGeo = null;
        internal static Coroutine starvePlayer = null;
        internal static bool depleteGeoRunning = false;
        internal static bool starvePlayerRunning = false;

        public List<IMenuMod.MenuEntry> GetMenuData(IMenuMod.MenuEntry? toggleButtonEntry)
        {
            return new List<IMenuMod.MenuEntry>
            {
                new IMenuMod.MenuEntry {
                    Name = "Geo rate",
                    Description = "How quickly you lose geo.",
                    Values = new string[] {
                        "1 geo per 0.5 seconds",
                        "1 geo per 1.0 second",
                        "1 geo per 1.5 seconds",
                        "1 geo per 2.0 seconds",
                        "1 geo per 2.5 seconds",
                        "1 geo per 3.0 seconds",
                        "1 geo per 3.5 seconds",
                        "1 geo per 4.0 seconds",
                        "1 geo per 4.5 seconds",
                        "1 geo per 5.0 seconds",
                    },
                    Saver = opt => GS.GeoDepleteOption = opt,
                    Loader = () => GS.GeoDepleteOption
                },
                new IMenuMod.MenuEntry {
                    Name = "Starve rate",
                    Description = "How quickly you lose health at 0 geo.",
                    Values = new string[] {
                        "1 mask per 2 seconds",
                        "1 mask per 3 seconds",
                        "1 mask per 4 seconds",
                        "1 mask per 5 seconds",
                        "1 mask per 6 seconds",
                        "1 mask per 7 seconds",
                        "1 mask per 8 seconds",
                        "1 mask per 9 seconds",
                        "1 mask per 10 seconds",
                    },
                    Saver = opt => GS.StarveOption = opt,
                    Loader = () => GS.StarveOption
                }
            };
        }

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            Log("Initializing");

            Instance = this;

            // If the mod is re-enabled during a game
            if (GameManager.instance.IsGameplayScene())
            {
                StartDepleting();
            }

            On.GameManager.StartNewGame += GameManager_StartNewGame;
            On.GameManager.LoadGame += GameManager_LoadGame;
            On.QuitToMenu.Start += OnQuitToMenu;

            Log("Initialized");
        }

        public void Unload()
        {
            StopDepleting();
            StopStarving();

            On.GameManager.StartNewGame -= GameManager_StartNewGame;
            On.GameManager.LoadGame -= GameManager_LoadGame;
            On.QuitToMenu.Start -= OnQuitToMenu;
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

        public IEnumerator OnQuitToMenu(On.QuitToMenu.orig_Start orig, QuitToMenu self)
        {
            StopDepleting();
            StopStarving();

            return orig(self);
        }

        public void StartDepleting()
        {
            // Don't do anything in God Seeker mode
            if (!depleteGeoRunning && !PlayerData.instance.bossRushMode)
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

                    yield return new WaitForSecondsRealtime((GS.GeoDepleteOption + 1) * 0.5f);

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

                if (PlayerData.instance.geo <= 0)
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

                yield return new WaitForSecondsRealtime(GS.StarveOption + 2.0f);

                if (CanTakeDamage())
                {
                    HeroController.instance.TakeDamage(null, GlobalEnums.CollisionSide.other, 1, 1);
                }
            }
        }
    }

    public class GlobalSettings
    {
        public int GeoDepleteOption = 2;
        public int StarveOption = 3;
    }
}