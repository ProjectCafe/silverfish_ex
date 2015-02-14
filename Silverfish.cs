using System;
using System.Collections.Generic;
using HSRangerLib;

namespace HREngine.Bots
{
	public class Silverfish
	{
		public string versionnumber = "114.0 (ex)";
		private bool singleLog = false;
		private string botbehave = "rush";
		public bool waitingForSilver = false;

		Playfield lastpf;
		Settings sttngs = Settings.Instance;

		List<Minion> ownMinions = new List<Minion>();
		List<Minion> enemyMinions = new List<Minion>();
		List<Handmanager.Handcard> handCards = new List<Handmanager.Handcard>();
		int ownPlayerController = 0;
		List<string> ownSecretList = new List<string>();
		int enemySecretCount = 0;
		List<int> enemySecretList = new List<int>();

		int currentMana = 0;
		int ownMaxMana = 0;
		int numOptionPlayedThisTurn = 0;
		int numMinionsPlayedThisTurn = 0;
		int cardsPlayedThisTurn = 0;
		int ueberladung = 0;

		int enemyMaxMana = 0;

		string ownHeroWeapon = "";
		int heroWeaponAttack = 0;
		int heroWeaponDurability = 0;

		string enemyHeroWeapon = "";
		int enemyWeaponAttack = 0;
		int enemyWeaponDurability = 0;

		string heroname = "";
		string enemyHeroname = "";

		CardDB.Card heroAbility = new CardDB.Card();
		bool ownAbilityisReady = false;
		CardDB.Card enemyAbility = new CardDB.Card();

		int anzcards = 0;
		int enemyAnzCards = 0;

		int ownHeroFatigue = 0;
		int enemyHeroFatigue = 0;
		int ownDecksize = 0;
		int enemyDecksize = 0;

		Minion ownHero;
		Minion enemyHero;

		private static GameState latestGameState;

		private static Silverfish instance;

		public static Silverfish Instance
		{
			get
			{
				return instance ?? (instance = new Silverfish());
			}
		}

		private Silverfish()
		{
			this.singleLog = Settings.Instance.writeToSingleFile;
			Helpfunctions.Instance.ErrorLog("init Silverfish");
			string path = SiverFishBotPath.AssemblyDirectory + System.IO.Path.DirectorySeparatorChar + "UltimateLogs" + System.IO.Path.DirectorySeparatorChar;
			System.IO.Directory.CreateDirectory(path);
			sttngs.setFilePath(SiverFishBotPath.AssemblyDirectory);

			Helpfunctions.Instance.ErrorLog(path);

			if (!singleLog)
			{
				sttngs.setLoggPath(path);
			}
			else
			{
				sttngs.setLoggPath(SiverFishBotPath.LogPath + System.IO.Path.DirectorySeparatorChar);
				sttngs.setLoggFile("UILogg.txt");
				Helpfunctions.Instance.createNewLoggfile();
			}
			PenalityManager.Instance.setCombos();
			Mulligan m = Mulligan.Instance; // read the mulligan list
		}

		public void setnewLoggFile()
		{
			if (!singleLog)
			{
				sttngs.setLoggFile("UILogg" + DateTime.Now.ToString("_yyyy-MM-dd_HH-mm-ss") + ".txt");
				Helpfunctions.Instance.createNewLoggfile();
				Helpfunctions.Instance.ErrorLog("#######################################################");
				Helpfunctions.Instance.ErrorLog("fight is logged in: " + sttngs.logpath + sttngs.logfile);
				Helpfunctions.Instance.ErrorLog("#######################################################");
			}
			else
			{
				sttngs.setLoggFile("UILogg.txt");
			}
		}

		public bool updateEverything(HSRangerLib.BotBase rangerbot,  Behavior botbase, bool runExtern = false, bool passiveWait = false)
		{
			latestGameState = rangerbot.gameState;

			this.updateBehaveString(botbase);

			Entity ownPlayer = rangerbot.FriendHero;
			Entity enemyPlayer = rangerbot.EnemyHero;
			ownPlayerController = ownPlayer.ControllerId;//ownPlayer.GetHero().GetControllerId()


			// create hero + minion data
			getHerostuff(rangerbot);
			getMinions(rangerbot);
			getHandcards(rangerbot);
			getDecks(rangerbot);

			// send ai the data:
			Hrtprozis.Instance.clearAll();
			Handmanager.Instance.clearAll();

			Hrtprozis.Instance.setOwnPlayer(ownPlayerController);
			Handmanager.Instance.setOwnPlayer(ownPlayerController);

			this.numOptionPlayedThisTurn = 0;
			this.numOptionPlayedThisTurn += this.cardsPlayedThisTurn +  this.ownHero.numAttacksThisTurn;
			foreach (Minion m in this.ownMinions)
			{
				if (m.Hp >= 1) this.numOptionPlayedThisTurn += m.numAttacksThisTurn;
			}

			Hrtprozis.Instance.updatePlayer(this.ownMaxMana, this.currentMana, this.cardsPlayedThisTurn, this.numMinionsPlayedThisTurn, this.numOptionPlayedThisTurn, this.ueberladung, ownHero.entitiyID, enemyHero.entitiyID);
			Hrtprozis.Instance.updateSecretStuff(this.ownSecretList, this.enemySecretCount);

			Hrtprozis.Instance.updateOwnHero(this.ownHeroWeapon, this.heroWeaponAttack, this.heroWeaponDurability, this.heroname, this.heroAbility, this.ownAbilityisReady, this.ownHero);
			Hrtprozis.Instance.updateEnemyHero(this.enemyHeroWeapon, this.enemyWeaponAttack, this.enemyWeaponDurability, this.enemyHeroname, this.enemyMaxMana, this.enemyAbility, this.enemyHero);

			Hrtprozis.Instance.updateMinions(this.ownMinions, this.enemyMinions);
			Handmanager.Instance.setHandcards(this.handCards, this.anzcards, this.enemyAnzCards);

			Hrtprozis.Instance.updateFatigueStats(this.ownDecksize, this.ownHeroFatigue, this.enemyDecksize, this.enemyHeroFatigue);

			Probabilitymaker.Instance.getEnemySecretGuesses(this.enemySecretList, Hrtprozis.Instance.heroNametoEnum(this.enemyHeroname));

			//learnmode :D

			Playfield p = new Playfield();



			if (lastpf != null)
			{
				if (lastpf.isEqualf(p))
				{
					return false;
				}

				//board changed we update secrets!
				//if(Ai.Instance.nextMoveGuess!=null) Probabilitymaker.Instance.updateSecretList(Ai.Instance.nextMoveGuess.enemySecretList);
				Probabilitymaker.Instance.updateSecretList(p, lastpf);
				lastpf = p;
			}
			else
			{
				lastpf = p;
			}

			p = new Playfield();//secrets have updated :D
			// calculate stuff
			Helpfunctions.Instance.ErrorLog("calculating stuff... " + DateTime.Now.ToString("HH:mm:ss.ffff"));
			if (runExtern)
			{
				Helpfunctions.Instance.logg("recalc-check###########");
				//p.printBoard();
				//Ai.Instance.nextMoveGuess.printBoard();
				if (p.isEqual(Ai.Instance.nextMoveGuess, true))
				{

					printstuff(rangerbot,false);
					Ai.Instance.doNextCalcedMove();

				}
				else
				{
					printstuff(rangerbot,true);
					readActionFile(passiveWait);
				}
			}
			else
			{
				printstuff(rangerbot,false);
				Ai.Instance.dosomethingclever(botbase);
			}

			Helpfunctions.Instance.ErrorLog("calculating ended! " + DateTime.Now.ToString("HH:mm:ss.ffff"));

			return true;
		}

		private void getHerostuff(HSRangerLib.BotBase rangerbot)
		{
			Dictionary<int, Entity> allEntitys = new Dictionary<int, Entity>();

			foreach (var item in rangerbot.gameState.GameEntityList)
			{
				allEntitys.Add(item.EntityId, item);
			}

			Entity ownPlayer = rangerbot.FriendHero;
			Entity enemyPlayer = rangerbot.EnemyHero;

			Entity ownhero = rangerbot.FriendHero;
			Entity enemyhero = rangerbot.EnemyHero;
			Entity ownHeroAbility = rangerbot.FriendHeroPower;

			//player stuff#########################
			//this.currentMana =ownPlayer.GetTag(HRGameTag.RESOURCES) - ownPlayer.GetTag(HRGameTag.RESOURCES_USED) + ownPlayer.GetTag(HRGameTag.TEMP_RESOURCES);
			this.currentMana = rangerbot.gameState.CurrentMana;
			this.ownMaxMana = rangerbot.gameState.LocalMaxMana;
			this.enemyMaxMana = rangerbot.gameState.RemoteMaxMana;
			enemySecretCount = rangerbot.EnemySecrets.Count;
			enemySecretCount = 0;
			//count enemy secrets
			enemySecretList.Clear();

			foreach (var item in rangerbot.EnemySecrets)
			{
				enemySecretList.Add(item.EntityId);
			}



			this.ownSecretList.Clear();

			foreach (var item in rangerbot.FriendSecrets)
			{
				this.ownSecretList.Add(item.CardId);
			}


			this.numMinionsPlayedThisTurn = rangerbot.gameState.NumMinionsPlayedThisTurn;
			this.cardsPlayedThisTurn = rangerbot.gameState.NumCardsPlayedThisTurn;
			this.ueberladung = rangerbot.gameState.RecallOwnedNum;

			//get weapon stuff
			this.ownHeroWeapon = "";
			this.heroWeaponAttack = 0;
			this.heroWeaponDurability = 0;

			this.ownHeroFatigue = ownhero.Fatigue;
			this.enemyHeroFatigue = enemyhero.Fatigue;

			this.ownDecksize = rangerbot.gameState.LocalDeckRemain;
			this.enemyDecksize = rangerbot.gameState.RemoteDeckRemain;


			//own hero stuff###########################
			int heroAtk = ownhero.ATK;
			int heroHp = ownhero.Health - ownhero.Damage;
			int heroDefence = ownhero.Armor;
			this.heroname = Hrtprozis.Instance.heroIDtoName(ownhero.CardId);

			bool heroImmuneToDamageWhileAttacking = false;
			bool herofrozen = ownhero.IsFrozen;
			int heroNumAttacksThisTurn = ownhero.NumAttacksThisTurn;
			bool heroHasWindfury = ownhero.HasWindfury;
			bool heroImmune = (ownhero.IsImmune);

			//Helpfunctions.Instance.ErrorLog(ownhero.GetName() + " ready params ex: " + exausted + " " + heroAtk + " " + numberofattacks + " " + herofrozen);


			if (rangerbot.FriendWeapon != null)
			{
				Entity weapon = rangerbot.FriendWeapon;
				this.ownHeroWeapon = CardDB.Instance.getCardDataFromID(CardDB.Instance.cardIdstringToEnum(rangerbot.FriendWeapon.CardId)).name.ToString();
				this.heroWeaponAttack = weapon.ATK;
				this.heroWeaponDurability = weapon.Durability - weapon.Damage;//weapon.GetDurability();
				heroImmuneToDamageWhileAttacking = false;
				if (this.ownHeroWeapon == "gladiatorslongbow")
				{
					heroImmuneToDamageWhileAttacking = true;
				}
				if (this.ownHeroWeapon == "doomhammer")
				{
					heroHasWindfury = true;
				}

				//Helpfunctions.Instance.ErrorLog("weapon: " + ownHeroWeapon + " " + heroWeaponAttack + " " + heroWeaponDurability);

			}



			//enemy hero stuff###############################################################
			this.enemyHeroname = Hrtprozis.Instance.heroIDtoName(enemyhero.CardId);

			int enemyAtk = enemyhero.ATK;
			int enemyHp = enemyhero.Health - enemyhero.Damage;
			int enemyDefence = enemyhero.Armor;
			bool enemyfrozen = enemyhero.IsFrozen;
			bool enemyHeroImmune = (enemyhero.IsImmune);

			this.enemyHeroWeapon = "";
			this.enemyWeaponAttack = 0;
			this.enemyWeaponDurability = 0;
			if (rangerbot.EnemyWeapon != null)
			{
				Entity weapon = rangerbot.EnemyWeapon;
				this.enemyHeroWeapon = CardDB.Instance.getCardDataFromID(CardDB.Instance.cardIdstringToEnum(weapon.CardId)).name.ToString();
				this.enemyWeaponAttack = weapon.ATK;
				this.enemyWeaponDurability = weapon.Durability;
			}


			//own hero ablity stuff###########################################################

			this.heroAbility = CardDB.Instance.getCardDataFromID(CardDB.Instance.cardIdstringToEnum(ownHeroAbility.CardId));
			this.ownAbilityisReady = (ownHeroAbility.IsExhausted) ? false : true; // if exhausted, ability is NOT ready
			this.enemyAbility = CardDB.Instance.getCardDataFromID(CardDB.Instance.cardIdstringToEnum(rangerbot.EnemyHeroPower.CardId));

			//generate Heros
			this.ownHero = new Minion();
			this.enemyHero = new Minion();
			this.ownHero.isHero = true;
			this.enemyHero.isHero = true;
			this.ownHero.own = true;
			this.enemyHero.own = false;
			this.ownHero.maxHp = ownhero.Health;
			this.enemyHero.maxHp = enemyhero.Health;
			this.ownHero.entitiyID = ownhero.EntityId;
			this.enemyHero.entitiyID = enemyhero.EntityId;

			this.ownHero.Angr = heroAtk;
			this.ownHero.Hp = heroHp;
			this.ownHero.armor = heroDefence;
			this.ownHero.frozen = herofrozen;
			this.ownHero.immuneWhileAttacking = heroImmuneToDamageWhileAttacking;
			this.ownHero.immune = heroImmune;
			this.ownHero.numAttacksThisTurn = heroNumAttacksThisTurn;
			this.ownHero.windfury = heroHasWindfury;

			this.enemyHero.Angr = enemyAtk;
			this.enemyHero.Hp = enemyHp;
			this.enemyHero.frozen = enemyfrozen;
			this.enemyHero.armor = enemyDefence;
			this.enemyHero.immune = enemyHeroImmune;
			this.enemyHero.Ready = false;

			this.ownHero.updateReadyness();


			//load enchantments of the heros
			List<miniEnch> miniEnchlist = new List<miniEnch>();
			foreach (Entity ent in allEntitys.Values)
			{
				if (ent.Attached == this.ownHero.entitiyID && ent.Zone == HSRangerLib.TAG_ZONE.PLAY)
				{
					CardDB.cardIDEnum id = CardDB.Instance.cardIdstringToEnum(ent.CardId);
					int controler = ent.ControllerId;
					int creator = ent.CreatorId;
					miniEnchlist.Add(new miniEnch(id, creator, controler));
				}

			}

			this.ownHero.loadEnchantments(miniEnchlist, ownhero.ControllerId);

			miniEnchlist.Clear();

			foreach (Entity ent in allEntitys.Values)
			{
				if (ent.Attached == this.enemyHero.entitiyID && ent.Zone == HSRangerLib.TAG_ZONE.PLAY)
				{
					CardDB.cardIDEnum id = CardDB.Instance.cardIdstringToEnum(ent.CardId);
					int controler = ent.ControllerId;
					int creator = ent.CreatorId;
					miniEnchlist.Add(new miniEnch(id, creator, controler));
				}

			}

			this.enemyHero.loadEnchantments(miniEnchlist, enemyhero.ControllerId);
			//fastmode weapon correction:
			if (ownHero.Angr < this.heroWeaponAttack) ownHero.Angr = this.heroWeaponAttack;
			if (enemyHero.Angr < this.enemyWeaponAttack) enemyHero.Angr = this.enemyWeaponAttack;

		}

		private void getMinions(HSRangerLib.BotBase rangerbot)
		{
			Dictionary<int, Entity> allEntitys = new Dictionary<int, Entity>();

			foreach (var item in rangerbot.gameState.GameEntityList)
			{
				allEntitys.Add(item.EntityId, item);
			}

			ownMinions.Clear();
			enemyMinions.Clear();
			Entity ownPlayer = rangerbot.FriendHero;
			Entity enemyPlayer = rangerbot.EnemyHero;

			// ALL minions on Playfield:
			List<Entity> list = new List<Entity>();

			foreach (var item in rangerbot.FriendMinion)
			{
				list.Add(item);
			}

			foreach (var item in rangerbot.EnemyMinion)
			{
				list.Add(item);
			}


			List<Entity> enchantments = new List<Entity>();


			foreach (Entity item in list)
			{
				Entity entitiy = item;
				int zp = entitiy.ZonePosition;

				if (entitiy.CardType == TAG_CARDTYPE.MINION && zp >= 1)
				{
					//Helpfunctions.Instance.ErrorLog("zonepos " + zp);
					CardDB.Card c = CardDB.Instance.getCardDataFromID(CardDB.Instance.cardIdstringToEnum(entitiy.CardId));
					Minion m = new Minion();
					m.name = c.name;
					m.handcard.card = c;
					m.Angr = entitiy.ATK;
					m.maxHp = entitiy.Health;
					m.Hp = m.maxHp - entitiy.Damage;
					if (m.Hp <= 0) continue;
					m.wounded = false;
					if (m.maxHp > m.Hp) m.wounded = true;


					m.exhausted = entitiy.IsExhausted;

					m.taunt = (entitiy.HasTaunt);

					m.numAttacksThisTurn = entitiy.NumAttacksThisTurn;

					int temp = entitiy.NumTurnsInPlay;
					m.playedThisTurn = (temp == 0) ? true : false;

					m.windfury = (entitiy.HasWindfury);

					m.frozen = (entitiy.IsFrozen);

					m.divineshild = (entitiy.HasDivineShield);

					m.stealth = (entitiy.IsStealthed);

					m.poisonous = (entitiy.IsPoisonous);

					m.immune = (entitiy.IsImmune);

					m.silenced = entitiy.IsSilenced;

					m.charge = 0;

					if (!m.silenced && m.name == CardDB.cardName.southseadeckhand && entitiy.HasCharge) m.charge = 1;
					if (!m.silenced && m.handcard.card.Charge) m.charge = 1;

					m.zonepos = zp;

					m.entitiyID = entitiy.EntityId;


					//Helpfunctions.Instance.ErrorLog(  m.name + " ready params ex: " + m.exhausted + " charge: " +m.charge + " attcksthisturn: " + m.numAttacksThisTurn + " playedthisturn " + m.playedThisTurn );


					List<miniEnch> enchs = new List<miniEnch>();
					foreach (Entity ent in allEntitys.Values)
					{
						if (ent.Attached == m.entitiyID && ent.Zone == HSRangerLib.TAG_ZONE.PLAY)
						{
							CardDB.cardIDEnum id = CardDB.Instance.cardIdstringToEnum(ent.CardId);
							int creator = ent.CreatorId;
							int controler = ent.ControllerId;
							enchs.Add(new miniEnch(id, creator, controler));
						}

					}

					m.loadEnchantments(enchs, entitiy.ControllerId);




					m.Ready = false; // if exhausted, he is NOT ready

					m.updateReadyness();


					if (entitiy.ControllerId == this.ownPlayerController) // OWN minion
					{
						m.own = true;
						this.ownMinions.Add(m);
					}
					else
					{
						m.own = false;
						this.enemyMinions.Add(m);
					}

				}
				// minions added

				/*
                if (entitiy.GetCardType() == HRCardType.WEAPON)
                {
                    //Helpfunctions.Instance.ErrorLog("found weapon!");
                    if (entitiy.GetControllerId() == this.ownPlayerController) // OWN weapon
                    {
                        this.ownHeroWeapon = CardDB.Instance.getCardDataFromID(CardDB.Instance.cardIdstringToEnum(entitiy.GetCardId())).name.ToString();
                        this.heroWeaponAttack = entitiy.GetATK();
                        this.heroWeaponDurability = entitiy.GetDurability();
                        //this.heroImmuneToDamageWhileAttacking = false;


                    }
                    else
                    {
                        this.enemyHeroWeapon = CardDB.Instance.getCardDataFromID(CardDB.Instance.cardIdstringToEnum(entitiy.GetCardId())).name.ToString();
                        this.enemyWeaponAttack = entitiy.GetATK();
                        this.enemyWeaponDurability = entitiy.GetDurability();
                    }
                }

                if (entitiy.GetCardType() == HRCardType.ENCHANTMENT)
                {

                    enchantments.Add(entitiy);
                }
                 */


			}

			/*foreach (HRCard item in list)
            {
                foreach (HREntity e in item.GetEntity().GetEnchantments())
                {
                    enchantments.Add(e);
                }
            }


            // add enchantments to minions
            setEnchantments(enchantments);*/
		}

		private void setEnchantments(List<Entity> enchantments)
		{
			/*
            foreach (HREntity bhu in enchantments)
            {
                //create enchantment
                Enchantment ench = CardDB.getEnchantmentFromCardID(CardDB.Instance.cardIdstringToEnum(bhu.GetCardId()));
                ench.creator = bhu.GetCreatorId();
                ench.controllerOfCreator = bhu.GetControllerId();
                ench.cantBeDispelled = false;
                //if (bhu.c) ench.cantBeDispelled = true;

                foreach (Minion m in this.ownMinions)
                {
                    if (m.entitiyID == bhu.GetAttached())
                    {
                        m.enchantments.Add(ench);
                        //Helpfunctions.Instance.ErrorLog("add enchantment " +bhu.GetCardId()+" to: " + m.entitiyID);
                    }

                }

                foreach (Minion m in this.enemyMinions)
                {
                    if (m.entitiyID == bhu.GetAttached())
                    {
                        m.enchantments.Add(ench);
                    }

                }

            }
            */
		}

		private void getHandcards(HSRangerLib.BotBase rangerbot)
		{
			handCards.Clear();
			this.anzcards = 0;
			this.enemyAnzCards = 0;
			List<Entity> list = rangerbot.FriendHand;

			foreach (Entity item in list)
			{

				Entity entitiy = item;

				if (entitiy.ControllerId == this.ownPlayerController && entitiy.ZonePosition >= 1) // own handcard
				{
					CardDB.Card c = CardDB.Instance.getCardDataFromID(CardDB.Instance.cardIdstringToEnum(entitiy.CardId));

					//c.cost = entitiy.GetCost();
					//c.entityID = entitiy.GetEntityId();

					Handmanager.Handcard hc = new Handmanager.Handcard();
					hc.card = c;
					hc.position = entitiy.ZonePosition;
					hc.entity = entitiy.EntityId;
					hc.manacost = entitiy.Cost;
					hc.addattack = 0;
					if (c.name == CardDB.cardName.bolvarfordragon)
					{
						hc.addattack = entitiy.ATK - 1; // -1 because it starts with 1, we count only the additional attackvalue
					}
					handCards.Add(hc);
					this.anzcards++;
				}


			}

			Dictionary<int, Entity> allEntitys = new Dictionary<int, Entity>();

			foreach (var item in rangerbot.gameState.GameEntityList)
			{
				allEntitys.Add(item.EntityId, item);
			}


			foreach (Entity ent in allEntitys.Values)
			{
				if (ent.ControllerId != this.ownPlayerController && ent.ZonePosition >= 1 && ent.Zone == HSRangerLib.TAG_ZONE.HAND) // enemy handcard
				{
					this.enemyAnzCards++;
				}
			}

		}

		private void getDecks(HSRangerLib.BotBase rangerbot)
		{
			Dictionary<int, Entity> allEntitys = new Dictionary<int, Entity>();

			foreach (var item in rangerbot.gameState.GameEntityList)
			{
				allEntitys.Add(item.EntityId, item);
			}

			int owncontroler = rangerbot.gameState.LocalControllerId;
			int enemycontroler = rangerbot.gameState.RemoteControllerId;
			List<CardDB.cardIDEnum> ownCards = new List<CardDB.cardIDEnum>();
			List<CardDB.cardIDEnum> enemyCards = new List<CardDB.cardIDEnum>();
			List<GraveYardItem> graveYard = new List<GraveYardItem>();

			foreach (Entity ent in allEntitys.Values)
			{
				if (ent.Zone == HSRangerLib.TAG_ZONE.SECRET && ent.ControllerId == enemycontroler) continue; // cant know enemy secrets :D
				if (ent.Zone == HSRangerLib.TAG_ZONE.DECK) continue;
				if (ent.CardType == HSRangerLib.TAG_CARDTYPE.MINION || ent.CardType == HSRangerLib.TAG_CARDTYPE.WEAPON || ent.CardType == HSRangerLib.TAG_CARDTYPE.ABILITY)
				{

					CardDB.cardIDEnum cardid = CardDB.Instance.cardIdstringToEnum(ent.CardId);
					//string owner = "own";
					//if (ent.GetControllerId() == enemycontroler) owner = "enemy";
					//if (ent.GetControllerId() == enemycontroler && ent.GetZone() == HRCardZone.HAND) Helpfunctions.Instance.logg("enemy card in hand: " + "cardindeck: " + cardid + " " + ent.GetName());
					//if (cardid != CardDB.cardIDEnum.None) Helpfunctions.Instance.logg("cardindeck: " + cardid + " " + ent.GetName() + " " + ent.GetZone() + " " + owner + " " + ent.GetCardType());
					if (cardid != CardDB.cardIDEnum.None)
					{
						if (ent.Zone == HSRangerLib.TAG_ZONE.GRAVEYARD)
						{
							GraveYardItem gyi = new GraveYardItem(cardid, ent.EntityId, ent.ControllerId == owncontroler);
							graveYard.Add(gyi);
						}

						int creator = ent.CreatorId;
						if (creator != 0 && creator != owncontroler && creator != enemycontroler) continue; //if creator is someone else, it was not played

						if (ent.ControllerId == owncontroler) //or controler?
						{
							if (ent.Zone == HSRangerLib.TAG_ZONE.GRAVEYARD)
							{
								ownCards.Add(cardid);
							}
						}
						else
						{
							if (ent.Zone == HSRangerLib.TAG_ZONE.GRAVEYARD)
							{
								enemyCards.Add(cardid);
							}
						}
					}
				}

			}

			Probabilitymaker.Instance.setOwnCards(ownCards);
			Probabilitymaker.Instance.setEnemyCards(enemyCards);
			bool isTurnStart = false;
			if (Ai.Instance.nextMoveGuess.mana == -100)
			{
				isTurnStart = true;
				Ai.Instance.updateTwoTurnSim();
			}
			Probabilitymaker.Instance.setGraveYard(graveYard, isTurnStart);

		}

		private void updateBehaveString(Behavior botbase)
		{
			this.botbehave = "rush";
			if (botbase is BehaviorControl) this.botbehave = "control";
			if (botbase is BehaviorMana) this.botbehave = "mana";
			this.botbehave += " " + Ai.Instance.maxwide;
			this.botbehave += " face " + ComboBreaker.Instance.attackFaceHP;
			if (Settings.Instance.secondTurnAmount > 0)
			{
				if (Ai.Instance.nextMoveGuess.mana == -100)
				{
					Ai.Instance.updateTwoTurnSim();
				}
				this.botbehave += " twoturnsim " + Settings.Instance.secondTurnAmount + " ntss " + Settings.Instance.nextTurnDeep + " " + Settings.Instance.nextTurnMaxWide + " " + Settings.Instance.nextTurnTotalBoards;
			}

			if (Settings.Instance.playarround)
			{
				this.botbehave += " playaround";
				this.botbehave += " " + Settings.Instance.playaroundprob + " " + Settings.Instance.playaroundprob2;
			}

			this.botbehave += " ets " + Settings.Instance.enemyTurnMaxWide;

			if (Settings.Instance.simEnemySecondTurn)
			{
				this.botbehave += " ets2 " + Settings.Instance.enemyTurnMaxWideSecondTime;
				this.botbehave += " ents " + Settings.Instance.enemySecondTurnMaxWide;
			}

			if (Settings.Instance.useSecretsPlayArround)
			{
				this.botbehave += " secret";
			}

			if (Settings.Instance.secondweight != 0.5f)
			{
				this.botbehave += " weight " + (int)(Settings.Instance.secondweight*100f);
			}

			if (Settings.Instance.simulatePlacement)
			{
				this.botbehave += " plcmnt";
			}


		}

		public static int getLastAffected(int entityid)
		{

			if (latestGameState != null)
			{
				foreach (var item in latestGameState.GameEntityList)
				{
					if (item.LastAffectedById == entityid)
					{
						return item.EntityId;
					}
				}
			}

			return 0;
		}

		public static int getCardTarget(int entityid)
		{

			if (latestGameState != null)
			{
				foreach (var item in latestGameState.GameEntityList)
				{
					if (item.EntityId == entityid)
					{
						return item.CardTargetId;
					}
				}
			}


			return 0;
		}

		//public void testExternal()
		//{
		//    BoardTester bt = new BoardTester("");
		//    this.currentMana = Hrtprozis.Instance.currentMana;
		//    this.ownMaxMana = Hrtprozis.Instance.ownMaxMana;
		//    this.enemyMaxMana = Hrtprozis.Instance.enemyMaxMana;
		//    printstuff(true);
		//    readActionFile();
		//}

		private void printstuff(HSRangerLib.BotBase rangerbot,bool runEx)
		{
			Entity ownPlayer = rangerbot.FriendHero;
			int ownsecretcount = rangerbot.FriendSecrets.Count;
			string dtimes = DateTime.Now.ToString("HH:mm:ss:ffff");
			string enemysecretIds = "";
			enemysecretIds = Probabilitymaker.Instance.getEnemySecretData();
			Helpfunctions.Instance.logg("#######################################################################");
			Helpfunctions.Instance.logg("#######################################################################");
			Helpfunctions.Instance.logg("start calculations, current time: " + DateTime.Now.ToString("HH:mm:ss") + " V" + this.versionnumber + " " + this.botbehave);
			Helpfunctions.Instance.logg("#######################################################################");
			Helpfunctions.Instance.logg("mana " + currentMana + "/" + ownMaxMana);
			Helpfunctions.Instance.logg("emana " + enemyMaxMana);
			Helpfunctions.Instance.logg("own secretsCount: " + ownsecretcount);

			Helpfunctions.Instance.logg("enemy secretsCount: " + enemySecretCount + " ;" + enemysecretIds);

			Ai.Instance.currentCalculatedBoard = dtimes;

			if (runEx)
			{
				Helpfunctions.Instance.resetBuffer();
				Helpfunctions.Instance.writeBufferToActionFile();
				Helpfunctions.Instance.resetBuffer();

				Helpfunctions.Instance.writeToBuffer("#######################################################################");
				Helpfunctions.Instance.writeToBuffer("#######################################################################");
				Helpfunctions.Instance.writeToBuffer("start calculations, current time: " + dtimes + " V" + this.versionnumber + " " + this.botbehave);
				Helpfunctions.Instance.writeToBuffer("#######################################################################");
				Helpfunctions.Instance.writeToBuffer("mana " + currentMana + "/" + ownMaxMana);
				Helpfunctions.Instance.writeToBuffer("emana " + enemyMaxMana);
				Helpfunctions.Instance.writeToBuffer("own secretsCount: " + ownsecretcount);
				Helpfunctions.Instance.writeToBuffer("enemy secretsCount: " + enemySecretCount + " ;" + enemysecretIds);
			}
			Hrtprozis.Instance.printHero(runEx);
			Hrtprozis.Instance.printOwnMinions(runEx);
			Hrtprozis.Instance.printEnemyMinions(runEx);
			Handmanager.Instance.printcards(runEx);
			Probabilitymaker.Instance.printTurnGraveYard(runEx);
			Probabilitymaker.Instance.printGraveyards(runEx);

			if (runEx) Helpfunctions.Instance.writeBufferToFile();

		}

		public bool readActionFile(bool passiveWaiting = false)
		{
			bool readed = true;
			List<string> alist = new List<string>();
			float value = 0f;
			string boardnumm = "-1";
			this.waitingForSilver = true;
			while (readed)
			{
				try
				{
					string data = System.IO.File.ReadAllText(Settings.Instance.path + "actionstodo.txt");
					if (data != "" && data != "<EoF>" && data.EndsWith("<EoF>"))
					{
						data = data.Replace("<EoF>", "");
						//Helpfunctions.Instance.ErrorLog(data);
						Helpfunctions.Instance.resetBuffer();
						Helpfunctions.Instance.writeBufferToActionFile();
						alist.AddRange(data.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries));
						string board = alist[0];
						if (board.StartsWith("board "))
						{
							boardnumm = (board.Split(' ')[1].Split(' ')[0]);
							alist.RemoveAt(0);
							if (boardnumm != Ai.Instance.currentCalculatedBoard)
							{
								if (passiveWaiting)
								{
									System.Threading.Thread.Sleep(10);
									return false;
								}
								continue;
							}
						}
						string first = alist[0];
						if (first.StartsWith("value "))
						{
							value = float.Parse((first.Split(' ')[1].Split(' ')[0]));
							alist.RemoveAt(0);
						}
						readed = false;
					}
					else
					{
						System.Threading.Thread.Sleep(10);
						if (passiveWaiting)
						{
							return false;
						}
					}

				}
				catch
				{
					System.Threading.Thread.Sleep(10);
				}

			}
			this.waitingForSilver = false;
			Helpfunctions.Instance.logg("received " + boardnumm + " actions to do:");
			Ai.Instance.currentCalculatedBoard = "0";
			Playfield p = new Playfield();
			List<Action> aclist = new List<Action>();

			foreach (string a in alist)
			{
				aclist.Add(new Action(a, p));
				Helpfunctions.Instance.logg(a);
			}

			Ai.Instance.setBestMoves(aclist, value);

			return true;
		}


	}

}

