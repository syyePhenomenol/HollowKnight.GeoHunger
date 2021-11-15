using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Modding;
using UnityEngine;
using HutongGames.PlayMaker;
using Modding.Menu;
using Modding.Menu.Config;
using Mono.Security.X509;

namespace GeoHunger
{
    public class GeoHunger : Mod, IGlobalSettings<GlobalSettings>, ICustomMenuMod, ITogglableMod
    {
        internal static GeoHunger Instance;

        private readonly string _version = "1.3.0";

        public override string GetVersion() => _version;

        public override int LoadPriority() => 2047;

        public static GlobalSettings GS { get; set; } = new GlobalSettings();

        public bool ToggleButtonInsideMenu => true;

        public void OnLoadGlobal(GlobalSettings s) => GS = s;

        public GlobalSettings OnSaveGlobal() => GS;

        internal static Coroutine depleteGeo = null;
        internal static Coroutine starvePlayer = null;
        internal static bool depleteGeoRunning = false;
        internal static bool starvePlayerRunning = false;
        internal static bool controlOverride = false;

        internal static List<GameObject> rampOptionsGo = new List<GameObject>();

        public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggleDelegates)
        {
            return new MenuBuilder(UIManager.instance.UICanvas.gameObject, "GeoHunger")
                .CreateTitle("GeoHunger", MenuTitleStyle.vanillaStyle)
                .CreateContentPane(RectTransformData.FromSizeAndPos(
                    new RelVector2(new Vector2(1920f, 903f)),
                    new AnchoredPosition(
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0f, -60f)
                    )
                ))
                .CreateControlPane(RectTransformData.FromSizeAndPos(
                    new RelVector2(new Vector2(1920f, 259f)),
                    new AnchoredPosition(
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0f, -502f)
                    )
                ))
                .SetDefaultNavGraph(new ChainedNavGraph()).AddContent(
                    RegularGridLayout.CreateVerticalLayout(105f),
                    c =>
                    {
                        c.AddHorizontalOption(
                            "Toggle Mod",
                            new HorizontalOptionConfig
                            {
                                Label = "Toggle Mod",
                                Options = new[] {"On", "Off"},
                                ApplySetting = (_, i) => { toggleDelegates.Value.SetModEnabled(i == 0); },
                                RefreshSetting = (s, _) => s.optionList.SetOptionTo( toggleDelegates.Value.GetModEnabled() ? 0 : 1),
                                CancelAction = _ => UIManager.instance.UIGoToDynamicMenu(modListMenu),
                                Style = HorizontalOptionStyle.VanillaStyle
                            });
                        c.AddHorizontalOption("Geo rate",
                                new HorizontalOptionConfig
                                {
                                    ApplySetting = (_, opt) => GS.GeoMinRate = opt,
                                    RefreshSetting = (s, _) => s.optionList.SetOptionTo(GS.GeoMinRate),
                                    CancelAction = _ => UIManager.instance.UIGoToDynamicMenu(modListMenu),
                                    Description = new DescriptionInfo
                                    {
                                        Text = "How quickly you lose geo.",
                                    },
                                    Label = "Geo rate",
                                    Options = new string[]
                                    {
                                        "0.2 geo per second",
                                        "0.3 geo per second",
                                        "0.4 geo per second",
                                        "0.6 geo per second",
                                        "0.8 geo per second",
                                        "1.0 geo per second",
                                        "1.5 geo per second",
                                        "2.0 geo per second",
                                    },
                                    Style = HorizontalOptionStyle.VanillaStyle
                                })
                            .AddHorizontalOption("Starve rate",
                                new HorizontalOptionConfig
                                {
                                    ApplySetting = (_, opt) => GS.StarveRate = opt,
                                    RefreshSetting = (s, _) => s.optionList.SetOptionTo(GS.StarveRate),
                                    CancelAction = _ => UIManager.instance.UIGoToDynamicMenu(modListMenu),
                                    Description = new DescriptionInfo
                                    {
                                        Text = "How quickly you lose health at 0 geo.",
                                    },
                                    Label = "Starve rate",
                                    Options = new string[]
                                    {
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
                                    Style = HorizontalOptionStyle.VanillaStyle
                                })
                            .AddHorizontalOption("Geo ramp",
                                new HorizontalOptionConfig
                                {
                                    ApplySetting = (_, opt) =>
                                    { 
                                        GS.GeoRampOn = opt;
                                        foreach (GameObject optionGo in rampOptionsGo)
                                        {
                                            optionGo.SetActive(opt == 1);
                                        }
                                    },
                                    RefreshSetting = (s, _) => s.optionList.SetOptionTo(GS.GeoRampOn),
                                    CancelAction = _ => UIManager.instance.UIGoToDynamicMenu(modListMenu),
                                    Description = new DescriptionInfo
                                    {
                                        Text = "Lose geo faster if you have more geo.",
                                    },
                                    Label = "Geo ramp",
                                    Options = new string[]
                                    {
                                        "Off",
                                        "On"
                                    },
                                    Style = HorizontalOptionStyle.VanillaStyle
                                })
                            .AddHorizontalOption("Ramp end",
                                new HorizontalOptionConfig
                                {
                                    ApplySetting = (_, opt) => GS.GeoRampEnd = opt,
                                    RefreshSetting = (s, _) => s.optionList.SetOptionTo(GS.GeoRampEnd),
                                    CancelAction = _ => UIManager.instance.UIGoToDynamicMenu(modListMenu),
                                    Description = new DescriptionInfo
                                    {
                                        Text = "The geo amount that ramping ends at.",
                                    },
                                    Label = "Ramp end",
                                    Options = new string[]
                                    {
                                        "100",
                                        "200",
                                        "500",
                                        "1000",
                                        "2000",
                                        "5000",
                                        "10000"
                                    },
                                    Style = HorizontalOptionStyle.VanillaStyle
                                }, out var rampEndGo)
                            .AddHorizontalOption("Ramp max rate",
                                new HorizontalOptionConfig
                                {
                                    ApplySetting = (_, opt) => GS.GeoMaxRate = opt,
                                    RefreshSetting = (s, _) => s.optionList.SetOptionTo(GS.GeoMaxRate),
                                    CancelAction = _ => UIManager.instance.UIGoToDynamicMenu(modListMenu),
                                    Description = new DescriptionInfo
                                    {
                                        Text = "The maximum depletion rate of geo (at ramp end).",

                                    },
                                    Label = "Ramp max rate",
                                    Options = new string[]
                                    {
                                        "5 geo per second",
                                        "10 geo per second",
                                        "20 geo per second",
                                        "50 geo per second",
                                        "100 geo per second"
                                    },
                                    Style = HorizontalOptionStyle.VanillaStyle
                                }, out var rampMaxRateGo);

                        rampOptionsGo.Clear();
                        rampOptionsGo.Add(rampEndGo.gameObject);
                        rampOptionsGo.Add(rampMaxRateGo.gameObject);
                        foreach (GameObject optionGo in rampOptionsGo)
                        {
                            optionGo.SetActive(GS.GeoRampOn == 1);
                        }

                    }).AddControls(
                    new SingleContentLayout(new AnchoredPosition(
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0f, -64f)
                    )), c => c.AddMenuButton(
                        "BackButton",
                        new MenuButtonConfig
                        {
                            Label = "Back",
                            CancelAction = _ => UIManager.instance.UIGoToDynamicMenu(modListMenu),
                            SubmitAction = _ => UIManager.instance.UIGoToDynamicMenu(modListMenu),
                            Style = MenuButtonStyle.VanillaStyle,
                            Proceed = true
                        })).Build();
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

            controlOverride = false;

            // Hook to save load and save quit
            On.GameManager.StartNewGame += GameManager_StartNewGame;
            On.GameManager.LoadGame += GameManager_LoadGame;
            On.QuitToMenu.Start += OnQuitToMenu;

            // We override controlReqlinquished for knight actions/casts
            On.PlayMakerFSM.OnEnable += PlayMakerFSM_OnEnable;

            Log("Initialized");
        }

        public void Unload()
        {
            StopDepleting();
            StopStarving();

            controlOverride = false;

            On.GameManager.StartNewGame -= GameManager_StartNewGame;
            On.GameManager.LoadGame -= GameManager_LoadGame;
            On.QuitToMenu.Start -= OnQuitToMenu;

            On.PlayMakerFSM.OnEnable -= PlayMakerFSM_OnEnable;
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

        public void PlayMakerFSM_OnEnable(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self)
        {
            orig(self);

            if (self.gameObject.name == "Knight"
                && (self.FsmName == "Surface Water"
                    || self.FsmName == "Superdash"
                    || self.FsmName == "Dream Nail"
                    || self.FsmName == "Nail Arts"
                    || self.FsmName == "Map Control"
                    || self.FsmName == "Spell Control"))
            {
                foreach (FsmState state in self.FsmStates)
                {
                    if (state.Name == "Take Control"
                        || state.Name == "Relinquish Control"
                        || state.Name == "Open Map"
                        || state.Name == "Focus Start"
                        || state.Name == "Scream Antic1"
                        || state.Name == "Scream Antic2"
                        || state.Name == "Quake Antic"
                        || state.Name == "Fireball Antic"
                        || state.Name == "Quake Antic")
                    {
                        state.Actions = state.Actions.Append(new SetControlOverride(true)).ToArray();
                    }
                    else if (state.Name == "Regain Control"
                        || state.Name == "Spell End")
                    {
                        state.Actions = state.Actions.Append(new SetControlOverride(false)).ToArray();
                    }
                }
            }
        }

        public class SetControlOverride : FsmStateAction
        {
            private readonly bool _value;

            public SetControlOverride(bool value)
            {
                _value = value;
            }

            public override void OnEnter()
            {
                controlOverride = _value;
                Finish();
            }
        }

        // Coroutines for depletion/starving behaviour
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

                    if (GS.GeoRampOn == 1)
                    {
                        yield return new WaitForSeconds(1.0f / GeoRampRate());
                    }
                    else
                    {
                        yield return new WaitForSeconds(1.0f / GeoMinRate());
                    }

                    if (CanTakeGeo())
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

                yield return new WaitForSeconds(GS.StarveRate + 2.0f);

                if (CanTakeGeo()
                    && !HeroController.instance.cState.invulnerable
                    && !HeroController.instance.cState.recoiling
                    && !HeroController.instance.playerData.GetBool("isInvincible"))
                {
                    HeroController.instance.TakeDamage(null, GlobalEnums.CollisionSide.other, 1, 1);
                }
            }
        }

        public bool CanTakeGeo()
        {
            return HeroController.instance.damageMode != GlobalEnums.DamageMode.NO_DAMAGE
                && HeroController.instance.transitionState == GlobalEnums.HeroTransitionState.WAITING_TO_TRANSITION
                && !HeroController.instance.cState.dead
                && !HeroController.instance.cState.hazardDeath
                && !BossSceneController.IsTransitioning
                && !HeroController.instance.cState.nearBench
                && !HeroController.instance.cState.isPaused
                && (!HeroController.instance.controlReqlinquished
                    || GameManager.instance.inventoryFSM.ActiveStateName == "Opened"
                    || controlOverride);
        }

        // Functions for calculating the geo depletion rate
        public float GeoRampRate()
        {
            if (PlayerData.instance.geo >= GeoRampEndAmount()) {
                return GeoMaxRate();
            }

            return GeoMinRate()
                + (GeoMaxRate() - GeoMinRate())
                * PlayerData.instance.geo / GeoRampEndAmount();
        }

        public float GeoMinRate()
        {
            return GS.GeoMinRate switch
            {
                0 => 0.2f,
                1 => 0.3f,
                2 => 0.4f,
                3 => 0.6f,
                4 => 0.8f,
                5 => 1.0f,
                6 => 1.5f,
                7 => 2.0f,
                _ => 0.2f
            };
        }

        public int GeoRampEndAmount()
        {
            return GS.GeoRampEnd switch
            {
                0 => 100,
                1 => 200,
                2 => 500,
                3 => 1000,
                4 => 2000,
                5 => 5000,
                6 => 10000,
                _ => 100
            };
        }

        public float GeoMaxRate()
        {
            return GS.GeoMaxRate switch
            {
                0 => 5.0f,
                1 => 10.0f,
                2 => 20.0f,
                3 => 50.0f,
                4 => 100.0f,
                _ => 5.0f
            };
        }
    }

    public class GlobalSettings
    {
        public int GeoMinRate = 3;
        public int StarveRate = 3;

        public int GeoRampOn = 0;
        public int GeoRampEnd = 4;
        public int GeoMaxRate = 1;
    }
}
