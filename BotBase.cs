using HSRangerLib;
using System;
using System.Collections.Generic;

namespace HREngine.Bots
{
	public static class SiverFishBotPath
	{
		public static string AssemblyDirectory
		{
			get
			{
				string codeBase = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
				UriBuilder uri = new UriBuilder(codeBase);
				string path = Uri.UnescapeDataString(uri.Path);
				return System.IO.Path.GetDirectoryName(path) + System.IO.Path.DirectorySeparatorChar;
			}
		}

		public static  string SettingsPath
		{
			get{
				string temp = AssemblyDirectory + System.IO.Path.DirectorySeparatorChar + "Common" + System.IO.Path.DirectorySeparatorChar;
				if (System.IO.Directory.Exists(temp) == false)
				{
					System.IO.Directory.CreateDirectory(temp);
				}

				return temp;
			}
		}

		public static string LogPath
		{
			get
			{
				string temp = AssemblyDirectory + System.IO.Path.DirectorySeparatorChar + "Logs" + System.IO.Path.DirectorySeparatorChar;
				if (System.IO.Directory.Exists(temp) == false)
				{
					System.IO.Directory.CreateDirectory(temp);
				}

				return temp;
			}
		}
	}

	public class Bot : BotBase
	{
		public override string Description
		{
			get
			{
				return "Author: __inline (based on Rush4xDev)"+"\r\n" +
					"This is the extended silver fish A.I. module.";
			}
		}

		//private int stopAfterWins = 30;
		private int concedeLvl = 5; // the rank, till you want to concede
		private int dirtytarget = -1;
		private int dirtychoice = -1;
		private string choiceCardId = "";
		DateTime starttime = DateTime.Now;
		Silverfish sf;


		Behavior behave;


		//
		bool isgoingtoconcede = false;
		int wins = 0;
		int loses = 0;

		public Bot()
		{

			//it's very important to set HasBestMoveAI property to true
			//or Hearthranger will never call OnQueryBestMove !
			base.HasBestMoveAI = true;

			silver.ConfigReader.Read ("cfg/settings.json");
			Helpfunctions.Instance.logg ("Loaded settings from cfg/settings.json");

			if(silver.ConfigReader.cfg.BehaviorControl)
			{
				behave = new BehaviorControl();
			}
			else if(silver.ConfigReader.cfg.BehaviorRush)
			{
				behave = new BehaviorRush ();
			}
			else
			{
				behave = new BehaviorMana ();
			}

			concedeLvl = silver.ConfigReader.cfg.concedeLvl;

			starttime = DateTime.Now;

			Settings set = Settings.Instance;
			this.sf = Silverfish.Instance;
			set.setSettings();
			sf.setnewLoggFile();
			CardDB cdb = CardDB.Instance;
			if (cdb.installedWrong)
			{
				Helpfunctions.Instance.ErrorLog("cant find CardDB");
				return;
			}

			bool teststuff = false; // set to true, to run a testfile (requires test.txt file in folder where _cardDB.txt file is located)
			bool printstuff = false; // if true, the best board of the tested file is printet stepp by stepp

			Helpfunctions.Instance.ErrorLog("----------------------------");
			Helpfunctions.Instance.ErrorLog("you are running uai V" + sf.versionnumber);
			Helpfunctions.Instance.ErrorLog("----------------------------");

			if (Settings.Instance.useExternalProcess) Helpfunctions.Instance.ErrorLog("YOU USE SILVER.EXE FOR CALCULATION, MAKE SURE YOU STARTED IT!");
			if (Settings.Instance.useExternalProcess) Helpfunctions.Instance.ErrorLog("SILVER.EXE IS LOCATED IN: " + Settings.Instance.path);



			if (teststuff)//run autotester for developpers
			{
				Ai.Instance.autoTester(printstuff);
			}
			writeSettings();
		}

		/// <summary>
		/// HRanger Code
		/// invoke when game enter mulligan
		/// </summary>
		/// <param name="e">
		///     e.card_list -- mulligan card list
		///     e.replace_list -- toggle card list (output)
		/// </param>
		public override void OnGameMulligan(GameMulliganEventArgs e)
		{
			if (e.handled)
			{
				return;
			}

			//set e.handled to true, 
			//then bot will toggle cards by e.replace_list 
			//and will not use internal mulligan logic anymore.
			e.handled = true;

			if (Settings.Instance.learnmode)
			{

				e.handled = false;
				return;
			}



			var list = e.card_list;

			Entity enemyPlayer = base.EnemyHero;
			Entity ownPlayer = base.FriendHero;

			string enemName = Hrtprozis.Instance.heroIDtoName(enemyPlayer.CardId);
			string ownName = Hrtprozis.Instance.heroIDtoName(ownPlayer.CardId);

			if (Mulligan.Instance.hasmulliganrules(ownName, enemName))
			{

				List<Mulligan.CardIDEntity> celist = new List<Mulligan.CardIDEntity>();
				foreach (var item in list)
				{
					if (item.CardId != "GAME_005")// dont mulligan coin
					{
						celist.Add(new Mulligan.CardIDEntity(item.CardId, item.EntityId));
					}
				}
				List<int> mullientitys = Mulligan.Instance.whatShouldIMulligan(celist, ownName, enemName);
				foreach (var item in list)
				{
					if (mullientitys.Contains(item.EntityId))
					{
						Helpfunctions.Instance.ErrorLog("Rejecting Mulligan Card " + HSRangerLib.CardDefDB.Instance.GetCardEnglishName(item.CardId) + " because of your rules");                            
						//toggle this card
						e.replace_list.Add(item);
					}
				}

			}
			else
			{
				foreach (var item in list)
				{
					if (item.Cost >= 4)
					{
						Helpfunctions.Instance.ErrorLog("Rejecting Mulligan Card " + HSRangerLib.CardDefDB.Instance.GetCardEnglishName(item.CardId) + " because it cost is >= 4.");

						e.replace_list.Add(item);

					}
					if (item.CardId == "EX1_308" || item.CardId == "EX1_622" || item.CardId == "EX1_005")
					{
						Helpfunctions.Instance.ErrorLog("Rejecting Mulligan Card " + HSRangerLib.CardDefDB.Instance.GetCardEnglishName(item.CardId) + " because it is soulfire or shadow word: death");
						e.replace_list.Add(item);
					}
				}
			}


			sf.setnewLoggFile();

			if (Mulligan.Instance.loserLoserLoser)
			{
				if (!autoconcede())
				{
					concedeVSenemy(ownName, enemName);
				}

				//set concede flag
				e.concede = this.isgoingtoconcede;
			}                
		}

		/// <summary>
		/// invoke when drafting arena cards (including hero draft)
		/// </summary>
		/// <param name="e"></param>
		public override void OnGameArenaDraft(GameArenaDraftEventArgs e)
		{
			//must set e.handled to true if you handle draft in this function.
			e.handled = false;
		}

		/// <summary>
		/// invoke when game starts.
		/// </summary>
		/// <param name="e">e.deck_list -- all cards id in the deck.</param>
		public override void OnGameStart(GameStartEventArgs e)
		{
			//do nothing here
		}

		/// <summary>
		/// invoke when game ends.
		/// </summary>
		/// <param name="e"></param>
		public override void OnGameOver(GameOverEventArgs e)
		{
			if (e.win)
			{
				HandleWining();
			}else if (e.loss || e.concede)
			{
				HandleLosing(e.concede);
			}
		}

		private HSRangerLib.BotAction CreateRangerConcedeAction()
		{
			HSRangerLib.BotAction ranger_action = new HSRangerLib.BotAction();
			ranger_action.Actor = base.FriendHero;
			ranger_action.Type = BotActionType.CONCEDE;

			return ranger_action;
		}

		private HSRangerLib.BotActionType GetRangerActionType(Entity actor, Entity target, actionEnum sf_action_type)
		{

			if (sf_action_type == actionEnum.endturn)
			{
				return BotActionType.END_TURN;
			}

			if (sf_action_type == actionEnum.useHeroPower)
			{
				return BotActionType.CAST_ABILITY;
			}

			if (sf_action_type == actionEnum.attackWithHero)
			{
				return BotActionType.HERO_ATTACK;
			}

			if (sf_action_type == actionEnum.attackWithMinion)
			{
				if (actor.Zone == HSRangerLib.TAG_ZONE.HAND && actor.IsMinion)
				{
					return BotActionType.CAST_MINION;
				}else if (actor.Zone == HSRangerLib.TAG_ZONE.PLAY && actor.IsMinion)
				{
					return BotActionType.MINION_ATTACK;
				}
			}

			if (sf_action_type == actionEnum.playcard)
			{
				if (actor.Zone == HSRangerLib.TAG_ZONE.HAND)
				{
					if (actor.IsMinion)
					{
						return BotActionType.CAST_MINION;
					}else if (actor.IsWeapon)
					{
						return BotActionType.CAST_WEAPON;
					}else
					{
						return BotActionType.CAST_SPELL;
					}                    
				}else if (actor.Zone == HSRangerLib.TAG_ZONE.PLAY)
				{
					if (actor.IsMinion)
					{
						return BotActionType.MINION_ATTACK;
					}else if (actor.IsWeapon)
					{
						return BotActionType.HERO_ATTACK;
					}
				}
			}

			if (target != null)
			{
				Helpfunctions.Instance.ErrorLog("GetActionType: wrong action type! " +
					sf_action_type.ToString() + ": " + HSRangerLib.CardDefDB.Instance.GetCardEnglishName(actor.CardId)
					+ " target: " + HSRangerLib.CardDefDB.Instance.GetCardEnglishName(target.CardId));
			}else
			{
				Helpfunctions.Instance.ErrorLog("GetActionType: wrong action type! " +
					sf_action_type.ToString() + ": " + HSRangerLib.CardDefDB.Instance.GetCardEnglishName(actor.CardId)
					+ " target none.");
			}


			return BotActionType.END_TURN;
		}

		private HSRangerLib.BotAction ConvertToRangerAction(Action moveTodo)
		{
			HSRangerLib.BotAction ranger_action = new HSRangerLib.BotAction();

			switch (moveTodo.actionType)
			{
			case actionEnum.endturn:
				break;
			case actionEnum.playcard:
				ranger_action.Actor = getCardWithNumber(moveTodo.card.entity);
				break;
			case actionEnum.attackWithHero:
				ranger_action.Actor = base.FriendHero;
				break;
			case actionEnum.useHeroPower:
				ranger_action.Actor = base.FriendHeroPower;
				break;
			case actionEnum.attackWithMinion:
				ranger_action.Actor = getEntityWithNumber(moveTodo.own.entitiyID);
				break;
			default:
				break;
			}

			if (moveTodo.target != null)
			{
				ranger_action.Target = getEntityWithNumber(moveTodo.target.entitiyID);
			}

			ranger_action.Type = GetRangerActionType(ranger_action.Actor, ranger_action.Target, moveTodo.actionType);

			if (moveTodo.druidchoice >= 1)
			{
				ranger_action.Choice = moveTodo.druidchoice;//1=leftcard, 2= rightcard
			}

			ranger_action.Index = moveTodo.place;


			if (moveTodo.target != null)
			{
				Helpfunctions.Instance.ErrorLog(moveTodo.actionType.ToString() + ": " + HSRangerLib.CardDefDB.Instance.GetCardEnglishName(ranger_action.Actor.CardId)
					+ " target: " + HSRangerLib.CardDefDB.Instance.GetCardEnglishName(ranger_action.Target.CardId));
				Helpfunctions.Instance.logg(moveTodo.actionType.ToString() + ": " + HSRangerLib.CardDefDB.Instance.GetCardEnglishName(ranger_action.Actor.CardId)
					+ " target: " + HSRangerLib.CardDefDB.Instance.GetCardEnglishName(ranger_action.Target.CardId)
					+ " choice: " + moveTodo.druidchoice);


			}
			else
			{
				Helpfunctions.Instance.ErrorLog(moveTodo.actionType.ToString() + ": " + HSRangerLib.CardDefDB.Instance.GetCardEnglishName(ranger_action.Actor.CardId)
					+ " target nothing");
				Helpfunctions.Instance.logg(moveTodo.actionType.ToString() + ": " + HSRangerLib.CardDefDB.Instance.GetCardEnglishName(ranger_action.Actor.CardId)
					+ " choice: " + moveTodo.druidchoice);
			}


			return ranger_action;
		}

		/// <summary>
		/// if uses extern a.i.,
		/// invoke when hearthranger did all the actions.
		/// </summary>
		/// <param name="e"></param>
		public override void OnQueryBestMove(QueryBestMoveEventArgs e)
		{

			//don't forget to set HasBestMoveAI property to true in class constructor.
			//or Hearthranger will never query best move !
			//base.HasBestMoveAI = true;

			e.handled = true;

			HSRangerLib.BotAction ranger_action ;

			try
			{

				if (this.isgoingtoconcede)
				{
					if (HSRangerLib.RangerBotSettings.CurrentSettingsGameType == HSRangerLib.enGameType.The_Arena)
					{
						this.isgoingtoconcede = false;
					}
					else
					{
						ranger_action = CreateRangerConcedeAction();
						e.action_list.Add(ranger_action);
						return;
					}
				}

				if (Settings.Instance.learnmode)
				{
					e.handled = false;
					return;
				}


				bool templearn = sf.updateEverything(this,behave, false, false);
				if (templearn == true) Settings.Instance.printlearnmode = true;



				if (Settings.Instance.learnmode)
				{
					if (Settings.Instance.printlearnmode)
					{
						Ai.Instance.simmulateWholeTurnandPrint();
					}
					Settings.Instance.printlearnmode = false;

					e.handled = false;
					return;
				}



				if (Ai.Instance.bestmoveValue <= -900 && Settings.Instance.enemyConcede) 
				{ 
					e.action_list.Add(CreateRangerConcedeAction());
					return;
				}

				Action moveTodo = Ai.Instance.bestmove;

				if (moveTodo == null || moveTodo.actionType == actionEnum.endturn)
				{
					//simply clear action list, hearthranger bot will endturn if no action can do.
					e.action_list.Clear();                    
					return;
				}

				Helpfunctions.Instance.ErrorLog("play action");
				moveTodo.print();

				e.action_list.Add(ConvertToRangerAction(moveTodo));
			}
			catch (Exception Exception)
			{
				Helpfunctions.Instance.ErrorLog(Exception.Message);
				Helpfunctions.Instance.ErrorLog(Environment.StackTrace);
				if (Settings.Instance.learnmode)
				{
					e.action_list.Clear();
					return;
				}
			}
			return ;
		}

		public override void OnActionDone(ActionDoneEventArgs e)
		{
			//do nothing here            
		}



		int lossedtodo = 0;
		int KeepConcede = 0;
		int oldwin = 0;
		private bool autoconcede()
		{
			if (HSRangerLib.RangerBotSettings.CurrentSettingsGameType == HSRangerLib.enGameType.The_Arena) return false;
			if (HSRangerLib.RangerBotSettings.CurrentSettingsGameType == HSRangerLib.enGameType.Play_Ranked) return false;
			int totalwin = this.wins;
			int totallose = this.loses;
			/*if ((totalwin + totallose - KeepConcede) != 0)
            {
                Helpfunctions.Instance.ErrorLog("#info: win:" + totalwin + " concede:" + KeepConcede + " lose:" + (totallose - KeepConcede) + " real winrate:" + (totalwin * 100 / (totalwin + totallose - KeepConcede)));
            }*/



			int curlvl = gameState.CurrentRank;

			if (curlvl > this.concedeLvl)
			{
				this.lossedtodo = 0;
				return false;
			}

			if (this.oldwin != totalwin)
			{
				this.oldwin = totalwin;
				if (this.lossedtodo > 0)
				{
					this.lossedtodo--;
				}
				Helpfunctions.Instance.ErrorLog("not today!! (you won a game)");
				this.isgoingtoconcede = true;
				return true;
			}

			if (this.lossedtodo > 0)
			{
				this.lossedtodo--;
				Helpfunctions.Instance.ErrorLog("not today!");
				this.isgoingtoconcede = true;
				return true;
			}

			if (curlvl < this.concedeLvl)
			{
				this.lossedtodo = 3;
				Helpfunctions.Instance.ErrorLog("your rank is " + curlvl + " targeted rank is " + this.concedeLvl + " -> concede!");
				Helpfunctions.Instance.ErrorLog("not today!!!");
				this.isgoingtoconcede = true;
				return true;
			}
			return false;
		}

		private bool concedeVSenemy(string ownh, string enemyh)
		{
			if (HSRangerLib.RangerBotSettings.CurrentSettingsGameType == HSRangerLib.enGameType.The_Arena) return false;
			if (HSRangerLib.RangerBotSettings.CurrentSettingsGameType == HSRangerLib.enGameType.Play_Ranked) return false;

			if (Mulligan.Instance.shouldConcede(Hrtprozis.Instance.heroNametoEnum(ownh), Hrtprozis.Instance.heroNametoEnum(enemyh)))
			{
				Helpfunctions.Instance.ErrorLog("not today!!!!");
				writeSettings();
				this.isgoingtoconcede = true;
				return true;
			}
			return false;
		}

		private void disableRelogger()
		{
			string version = sf.versionnumber;
			int totalwin = 0;
			int totallose = 0;
			string[] lines = new string[0] { };
			try
			{
				string path = SiverFishBotPath.SettingsPath;
				//string path = (HRSettings.Get.CustomRuleFilePath).Remove(HRSettings.Get.CustomRuleFilePath.Length - 13) + "Common" + System.IO.Path.DirectorySeparatorChar;
				lines = System.IO.File.ReadAllLines(path + "Settings.ini");
			}
			catch
			{
				Helpfunctions.Instance.logg("cant find Settings.ini");
			}
			List<string> newlines = new List<string>();
			for (int i = 0; i < lines.Length; i++)
			{
				string s = lines[i];

				if (s.Contains("client.relogger"))
				{
					s = "client.relogger=false";
				}
				//Helpfunctions.Instance.ErrorLog("add " + s);
				newlines.Add(s);

			}


			try
			{
				string path = SiverFishBotPath.SettingsPath;
				System.IO.File.WriteAllLines(path + "Settings.ini", newlines.ToArray());
			}
			catch
			{
				Helpfunctions.Instance.logg("cant write Settings.ini");
			}
		}

		private void writeSettings()
		{
			string version = sf.versionnumber;
			string[] lines = new string[0] { };
			try
			{
				string path = SiverFishBotPath.SettingsPath;
				lines = System.IO.File.ReadAllLines(path + "Settings.ini");
			}
			catch
			{
				Helpfunctions.Instance.logg("cant find Settings.ini");
			}
			List<string> newlines = new List<string>();
			for (int i = 0; i < lines.Length; i++)
			{
				string s = lines[i];

				if (s.Contains("uai.version"))
				{
					s = "uai.version=V" + version;
				}

				if (s.Contains("uai.concedes"))
				{
					s = "uai.concedes=" + KeepConcede;
				}

				if (s.Contains("uai.wins"))
				{
					s = "uai.wins=" + this.wins;
				}
				if (s.Contains("uai.loses"))
				{
					s = "uai.loses=" + this.loses;
				}
				if (s.Contains("uai.winrate"))
				{
					s = "uai.winrate=" + 0;
					double winr = 0;
					if ((this.wins + this.loses - KeepConcede) != 0)
					{
						winr = ((double)(this.wins * 100) / (double)(this.wins + this.loses - KeepConcede));
						s = "uai.winrate=" + Math.Round(winr, 2);
					}

				}
				if (s.Contains("uai.winph"))
				{
					s = "uai.winph=" + 0;
					double winh = 0;
					if ((DateTime.Now - starttime).TotalHours >= 0.001)
					{

						winh = (double)this.wins / (DateTime.Now - starttime).TotalHours;
						s = "uai.winph=" + Math.Round(winh, 2);
					}

				}
				//Helpfunctions.Instance.ErrorLog("add " + s);
				newlines.Add(s);

			}


			try
			{
				string path = SiverFishBotPath.SettingsPath;
				System.IO.File.WriteAllLines(path + "Settings.ini", newlines.ToArray());
			}
			catch
			{
				Helpfunctions.Instance.logg("cant write Settings.ini");
			}
		}

		private void resetSettings()
		{
			string[] lines = new string[0] { };
			try
			{
				string path = SiverFishBotPath.SettingsPath;
				lines = System.IO.File.ReadAllLines(path + "Settings.ini");
			}
			catch
			{
				Helpfunctions.Instance.logg("cant find Settings.ini");
			}
			List<string> newlines = new List<string>();
			for (int i = 0; i < lines.Length; i++)
			{
				string s = lines[i];

				if (s.Contains("uai.reset"))
				{
					s = "uai.reset=false";
				}

				if (s.Contains("uai.extern"))
				{
					s = "uai.extern=true";
				}
				if (s.Contains("uai.passivewait"))
				{
					s = "uai.passivewait=true";
				}
				if (s.Contains("uai.wwuaid"))
				{
					s = "uai.wwuaid=false";
				}
				if (s.Contains("uai.enemyfacehp"))
				{
					s = "uai.enemyfacehp=15";
				}
				if (s.Contains("uai.singleLog"))
				{
					s = "uai.singleLog=false";
				}


				// advanced settings
				if (s.Contains("uai.maxwide"))
				{
					s = "uai.maxwide=4000";
				}

				if (s.Contains("uai.maxBoardsEnemysTurn"))
				{
					s = "uai.maxBoardsEnemysTurn=40";
				}

				if (s.Contains("uai.simulateTwoTurnCounter"))
				{
					s = "uai.simulateTwoTurnCounter=1500";
				}

				if (s.Contains("uai.maxBoardsEnemysTurnSecondStepp"))
				{
					s = "uai.maxBoardsEnemysTurnSecondStepp=200";
				}

				if (s.Contains("uai.nextTurnSimDeep"))
				{
					s = "uai.nextTurnSimDeep=6";
				}
				if (s.Contains("uai.nextTurnSimWide"))
				{
					s = "uai.nextTurnSimWide=20";
				}
				if (s.Contains("uai.nextTurnSimBoards"))
				{
					s = "uai.nextTurnSimBoards=200";
				}

				if (s.Contains("uai.simulateEnemyOnSecondTurn"))
				{
					s = "uai.simulateEnemyOnSecondTurn=true";
				}
				if (s.Contains("uai.maxBoardsEnemysSecondTurn"))
				{
					s = "uai.maxBoardsEnemysSecondTurn=20";
				}

				if (s.Contains("uai.placement"))
				{
					s = "uai.placement=true";
				}

				if (s.Contains("uai.secrets"))
				{
					s = "uai.secrets=false";
				}

				if (s.Contains("uai.playAround"))
				{
					s = "uai.playAround=false";
				}
				if (s.Contains("uai.playAroundProb"))
				{
					s = "uai.playAroundProb=40";
				}
				if (s.Contains("uai.playAroundProb2"))
				{
					s = "uai.playAroundProb2=80";
				}

				if (s.Contains("uai.secondweight"))
				{
					s = "uai.secondweight=50";
				}
				newlines.Add(s);
				Helpfunctions.Instance.logg("add " +s);

			}


			try
			{
				string path = SiverFishBotPath.SettingsPath;
				System.IO.File.WriteAllLines(path + "Settings.ini", newlines.ToArray());
			}
			catch
			{
				Helpfunctions.Instance.logg("cant write Settings.ini");
			}
		}

		private void HandleWining()
		{
			this.wins++;
			if (this.isgoingtoconcede)
			{
				this.isgoingtoconcede = false;
			}
			writeSettings();
			writeTrigger(1);
			int totalwin = this.wins;
			int totallose = this.loses;
			if ((totalwin + totallose - KeepConcede) != 0)
			{
				Helpfunctions.Instance.ErrorLog("#info: win:" + totalwin + " concede:" + KeepConcede + " lose:" + (totallose - KeepConcede) + " real winrate:" + (totalwin * 100 / (totalwin + totallose - KeepConcede)));
			}
			else
			{
				Helpfunctions.Instance.ErrorLog("#info: win:" + totalwin + " concede:" + KeepConcede + " lose:" + (totallose - KeepConcede) + " real winrate: infinity!!!! (division by zero :D)");
			}            
		}

		private void HandleLosing(bool is_concede)
		{
			this.loses++;
			if (is_concede)
			{
				this.isgoingtoconcede = false;
				writeTrigger(0);
				this.KeepConcede++;
			}
			else
			{
				writeTrigger(2);
			}
			writeSettings();
			int totalwin = this.wins;
			int totallose = this.loses;
			if ((totalwin + totallose - KeepConcede) != 0)
			{
				Helpfunctions.Instance.ErrorLog("#info: win:" + totalwin + " concede:" + KeepConcede + " lose:" + (totallose - KeepConcede) + " real winrate:" + (totalwin * 100 / (totalwin + totallose - KeepConcede)));
			}
			else
			{
				Helpfunctions.Instance.ErrorLog("#info: win:" + totalwin + " concede:" + KeepConcede + " lose:" + (totallose - KeepConcede) + " real winrate: infinity!!!! (division by zero :D)");
			}

		}

		private void writeTrigger(int what)
		{
			try
			{
				string path = SiverFishBotPath.SettingsPath + System.IO.Path.DirectorySeparatorChar + "uaibattletrigger.txt";
				string w = "concede";
				if (what == 1) w = "win";
				if (what == 2) w = "loss";
				System.IO.File.WriteAllText(path, w);
			}
			catch
			{
				Helpfunctions.Instance.logg("cant write trigger");
			}
		}

		private Entity getEntityWithNumber(int number)
		{
			foreach (Entity e in gameState.GameEntityList)
			{
				if (number == e.EntityId) return e;
			}
			return null;
		}

		private Entity getCardWithNumber(int number)
		{
			foreach (Entity e in base.FriendHand)
			{
				if (number == e.EntityId) return e;
			}
			return null;
		}

		private List<Entity> getallEntitys()
		{

			List<Entity> result = gameState.GameEntityList;

			return result;
		}

		private List<Entity> getallHandCards()
		{            
			return base.FriendHand;
		}


	}
}