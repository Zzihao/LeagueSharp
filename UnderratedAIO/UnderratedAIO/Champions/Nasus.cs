﻿using System;
using System.Collections.Generic;
using System.Linq;
using Color = System.Drawing.Color;
using LeagueSharp;
using LeagueSharp.Common;
using UnderratedAIO.Helpers;
using Environment = UnderratedAIO.Helpers.Environment;
using Orbwalking = UnderratedAIO.Helpers.Orbwalking;

namespace UnderratedAIO.Champions
{
    internal class Nasus
    {
        public static Menu config;
        public static Orbwalking.Orbwalker orbwalker;
        public static AutoLeveler autoLeveler;
        public static Spell Q, W, E, R;
        private static float lastR;
        public static readonly Obj_AI_Hero player = ObjectManager.Player;

        public Nasus()
        {
            if (player.BaseSkinName != "Nasus")
            {
                return;
            }
            InitNocturne();
            InitMenu();
            Game.PrintChat("<font color='#9933FF'>Soresu </font><font color='#FFFFFF'>- Nasus</font>");
            Drawing.OnDraw += Game_OnDraw;
            Game.OnUpdate += Game_OnGameUpdate;
            Helpers.Jungle.setSmiteSlot();
            Utility.HpBarDamageIndicator.DamageToUnit = ComboDamage;
            Orbwalking.BeforeAttack += Orbwalking_BeforeAttack;
            Orbwalking.OnAttack += Orbwalking_OnAttack;
        }

        private void Orbwalking_OnAttack(AttackableUnit unit, AttackableUnit target)
        {
            if (unit.IsMe && Q.IsReady() && config.Item("autoQ").GetValue<bool>() &&
                target.Health < Q.GetDamage((Obj_AI_Base) target) + player.GetAutoAttackDamage((Obj_AI_Base) target))
            {
                Q.Cast(config.Item("packets").GetValue<bool>());
            }
        }

        private void Orbwalking_BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            if (Q.IsReady() &&
                ((args.Target is Obj_AI_Hero && orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo) ||
                 args.Target.Health <
                 Q.GetDamage((Obj_AI_Base) args.Target) + player.GetAutoAttackDamage((Obj_AI_Base) args.Target)))
            {
                Q.Cast(config.Item("packets").GetValue<bool>());
            }
        }

        private void Game_OnGameUpdate(EventArgs args)
        {
            Obj_AI_Hero target = TargetSelector.GetTarget(950, TargetSelector.DamageType.Physical);
            switch (orbwalker.ActiveMode)
            {
                case Orbwalking.OrbwalkingMode.Combo:
                    Combo(target);
                    break;
                case Orbwalking.OrbwalkingMode.Mixed:
                    Harass(target);
                    break;
                case Orbwalking.OrbwalkingMode.LaneClear:
                    Clear();
                    break;
                case Orbwalking.OrbwalkingMode.LastHit:
                    if (Q.IsReady())
                    {
                        useQ();
                    }
                    break;
                default:
                    break;
            }
            Jungle.CastSmite(config.Item("useSmite").GetValue<KeyBind>().Active);
            if (config.Item("QSSEnabled").GetValue<bool>())
            {
                ItemHandler.UseCleanse(config);
            }
        }

        private void Combo(Obj_AI_Hero target)
        {
            if (target == null)
            {
                return;
            }
            var cmbdmg = ComboDamage(target) + ItemHandler.GetItemsDamage(target);
            if (config.Item("useItems").GetValue<bool>())
            {
                ItemHandler.UseItems(target, config, cmbdmg);
            }
            if (config.Item("usee").GetValue<bool>() && E.IsReady() &&
                ((config.Item("useeslow").GetValue<bool>() && NasusW(target)) || !config.Item("useeslow").GetValue<bool>()))
            {
                if (E.CanCast(target))
                {
                    E.Cast(target, config.Item("packets").GetValue<bool>());
                }
                else
                {
                    var ePred = E.GetPrediction(target);
                    if (ePred.CastPosition.Distance(player.Position) < 925)
                    {
                        E.Cast(
                            player.Position.Extend(target.Position, E.Range), config.Item("packets").GetValue<bool>());
                    }
                }
            }
            if (config.Item("usew").GetValue<bool>() && W.CanCast(target))
            {
                W.Cast(target, config.Item("packets").GetValue<bool>());
            }
            if (!config.Item("Rdamage").GetValue<bool>())
            {
                cmbdmg += R.GetDamage(target) * 15;
            }
            var bonusDmg = Environment.Hero.GetAdOverFive(target);
            if ((config.Item("user").GetValue<bool>() && player.Distance(target) < player.AttackRange + 50 &&
                 cmbdmg + bonusDmg > target.Health && target.Health > bonusDmg + 200 && player.HealthPercent < 50) ||
                (config.Item("usertf").GetValue<Slider>().Value <= player.CountEnemiesInRange(600) &&
                 player.HealthPercent < 80))
            {
                R.Cast(config.Item("packets").GetValue<bool>());
            }
            var ignitedmg = (float) player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);
            bool hasIgnite = player.Spellbook.CanUseSpell(player.GetSpellSlot("SummonerDot")) == SpellState.Ready;
            if (config.Item("useIgnite").GetValue<bool>() && ignitedmg > target.Health && hasIgnite &&
                !E.CanCast(target))
            {
                player.Spellbook.CastSpell(player.GetSpellSlot("SummonerDot"), target);
            }
        }

        private void Clear()
        {
            if (Q.IsReady())
            {
                useQ();
            }
            if (NasusQ && player.CountEnemiesInRange(Orbwalking.GetRealAutoAttackRange(player)) == 0)
            {
                var minion =
                    MinionManager.GetMinions(
                        Orbwalking.GetRealAutoAttackRange(player), MinionTypes.All, MinionTeam.NotAlly)
                        .FirstOrDefault(m => m.Health < Q.GetDamage(m) + player.GetAutoAttackDamage(m));
                orbwalker.ForceTarget(minion);
            }
            float perc = config.Item("minmana").GetValue<Slider>().Value / 100f;
            if (player.Mana < player.MaxMana * perc)
            {
                return;
            }
            MinionManager.FarmLocation bestPositionE =
                E.GetCircularFarmLocation(MinionManager.GetMinions(E.Range, MinionTypes.All, MinionTeam.NotAlly));
            if (config.Item("useeLC").GetValue<bool>() && Q.IsReady() &&
                bestPositionE.MinionsHit >= config.Item("ehitLC").GetValue<Slider>().Value)
            {
                E.Cast(bestPositionE.Position, config.Item("packets").GetValue<bool>());
            }
        }

        private void useQ()
        {
            var minions =
                MinionManager.GetMinions(Orbwalking.GetRealAutoAttackRange(player), MinionTypes.All, MinionTeam.NotAlly)
                    .FirstOrDefault(m => m.Health < Q.GetDamage(m) + player.GetAutoAttackDamage(m));
            if (minions != null)
            {
                Q.Cast(config.Item("packets").GetValue<bool>());
            }
        }

        private void Harass(Obj_AI_Hero target)
        {
            if (Q.IsReady())
            {
                useQ();
            }
            float perc = config.Item("minmanaH").GetValue<Slider>().Value / 100f;
            if (player.Mana < player.MaxMana * perc)
            {
                return;
            }
            if (target == null)
            {
                return;
            }
            if (config.Item("useeH").GetValue<bool>() && E.IsReady())
            {
                if (E.CanCast(target))
                {
                    E.Cast(target, config.Item("packets").GetValue<bool>());
                }
                else
                {
                    var ePred = E.GetPrediction(target);
                    if (ePred.CastPosition.Distance(player.Position) < 1000)
                    {
                        E.Cast(
                            player.Position.Extend(target.Position, E.Range), config.Item("packets").GetValue<bool>());
                    }
                }
            }
        }

        private static float ComboDamage(Obj_AI_Hero hero)
        {
            double damage = 0;
            if (E.IsReady() && E.Instance.ManaCost < player.Mana)
            {
                damage += Damage.GetSpellDamage(player, hero, SpellSlot.E);
            }
            if (R.IsReady() && config.Item("Rdamage").GetValue<bool>())
            {
                damage += Damage.GetSpellDamage(player, hero, SpellSlot.R) * 15;
            }
            //damage += ItemHandler.GetItemsDamage(hero);
            var ignitedmg = player.GetSummonerSpellDamage(hero, Damage.SummonerSpell.Ignite);
            if (player.Spellbook.CanUseSpell(player.GetSpellSlot("summonerdot")) == SpellState.Ready &&
                hero.Health < damage + ignitedmg)
            {
                damage += ignitedmg;
            }
            return (float) damage;
        }

        private void Game_OnDraw(EventArgs args)
        {
            DrawHelper.DrawCircle(config.Item("drawee", true).GetValue<Circle>(), E.Range);
            DrawHelper.DrawCircle(config.Item("drawww", true).GetValue<Circle>(), W.Range);
            DrawHelper.DrawCircle(config.Item("drawrr", true).GetValue<Circle>(), R.Range);
            Helpers.Jungle.ShowSmiteStatus(
                config.Item("useSmite").GetValue<KeyBind>().Active, config.Item("smiteStatus").GetValue<bool>());
            Utility.HpBarDamageIndicator.Enabled = config.Item("drawcombo").GetValue<bool>();
        }

        private void InitNocturne()
        {
            Q = new Spell(SpellSlot.Q);
            W = new Spell(SpellSlot.W, 550);
            E = new Spell(SpellSlot.E, 600);
            E.SetSkillshot(
                E.Instance.SData.SpellCastTime, E.Instance.SData.LineWidth, E.Speed, false,
                SkillshotType.SkillshotCircle);
            R = new Spell(SpellSlot.R, 175);
        }

        private static bool NasusW(Obj_AI_Hero target)
        {
            return target.Buffs.Any(buff => buff.Name == "NasusW");
        }

        private static bool NasusQ
        {
            get { return player.Buffs.Any(buff => buff.Name == "NasusQ"); }
        }

        private void InitMenu()
        {
            config = new Menu("Nasus ", "Nasus", true);
            // Target Selector
            Menu menuTS = new Menu("Selector", "tselect");
            TargetSelector.AddToMenu(menuTS);
            config.AddSubMenu(menuTS);
            // Orbwalker
            Menu menuOrb = new Menu("Orbwalker", "orbwalker");
            orbwalker = new Orbwalking.Orbwalker(menuOrb);
            config.AddSubMenu(menuOrb);
            // Draw settings
            Menu menuD = new Menu("Drawings ", "dsettings");
            menuD.AddItem(new MenuItem("drawww", "Draw W range", true))
                .SetValue(new Circle(false, Color.FromArgb(180, 100, 146, 166)));
            menuD.AddItem(new MenuItem("drawee", "Draw E range", true))
                .SetValue(new Circle(false, Color.FromArgb(180, 100, 146, 166)));
            menuD.AddItem(new MenuItem("drawrr", "Draw R range", true))
                .SetValue(new Circle(false, Color.FromArgb(180, 100, 146, 166)));
            menuD.AddItem(new MenuItem("drawcombo", "Draw combo damage")).SetValue(true);
            config.AddSubMenu(menuD);
            Menu menuC = new Menu("Combo ", "csettings");
            menuC.AddItem(new MenuItem("useq", "Use Q")).SetValue(true);
            menuC.AddItem(new MenuItem("usew", "Use W")).SetValue(true);
            menuC.AddItem(new MenuItem("usee", "Use E")).SetValue(true);
            menuC.AddItem(new MenuItem("useeslow", "Use E only for slowed enemy")).SetValue(true);
            menuC.AddItem(new MenuItem("user", "Use R in 1v1")).SetValue(true);
            menuC.AddItem(new MenuItem("usertf", "R min enemy in teamfight")).SetValue(new Slider(2, 1, 5));
            menuC.AddItem(new MenuItem("useIgnite", "Use Ignite")).SetValue(true);
            menuC = ItemHandler.addItemOptons(menuC);
            config.AddSubMenu(menuC);
            // Harass Settings
            Menu menuH = new Menu("Harass ", "Hsettings");
            menuH.AddItem(new MenuItem("useqH", "Use Q")).SetValue(true);
            menuH.AddItem(new MenuItem("useeH", "Use E")).SetValue(true);
            menuH.AddItem(new MenuItem("minmanaH", "Keep X% mana")).SetValue(new Slider(1, 1, 100));
            config.AddSubMenu(menuH);
            // LaneClear Settings
            Menu menuLC = new Menu("LaneClear ", "Lcsettings");
            menuLC.AddItem(new MenuItem("useeLC", "Use E")).SetValue(true);
            menuLC.AddItem(new MenuItem("ehitLC", "   Min hit").SetValue(new Slider(4, 1, 10)));
            menuLC.AddItem(new MenuItem("minmana", "Keep X% mana")).SetValue(new Slider(1, 1, 100));
            config.AddSubMenu(menuLC);
            Menu menuM = new Menu("Misc ", "Msettings");
            menuM = Jungle.addJungleOptions(menuM);
            menuM = ItemHandler.addCleanseOptions(menuM);
            menuM.AddItem(new MenuItem("autoQ", "Auto Q")).SetValue(true);
            menuM.AddItem(new MenuItem("Rdamage", "Combo damage with R")).SetValue(true);
            Menu autolvlM = new Menu("AutoLevel", "AutoLevel");
            autoLeveler = new AutoLeveler(autolvlM);
            menuM.AddSubMenu(autolvlM);

            config.AddSubMenu(menuM);
            config.AddItem(new MenuItem("packets", "Use Packets")).SetValue(false);
            config.AddItem(new MenuItem("UnderratedAIO", "by Soresu v" + Program.version.ToString().Replace(",", ".")));
            config.AddToMainMenu();
        }
    }
}