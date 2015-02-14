namespace silver
{
	/// <summary>
	/// Contains all values of the config
	/// </summary>
	public class Config
	{	
		public bool BehaviorControl { get; set; }
		public bool BehaviorMana { get; set; }
		public bool BehaviorRush { get; set; }

		public bool playaroundtraps { get; set; }
		public bool playaroundaoe { get; set; }
		public bool simulatePlacement { get; set; }

		public int enfacehp { get; set; }
		public int maxwide { get; set; }
		public int playaroundprob { get; set; }
		public int playaroundprob2 { get; set; }
		public int enemyTurnMaxWide { get; set; }
		public int enemyTurnMaxWideSecondTime { get; set; }
		public int enemySecondTurnMaxWide { get; set; }
		public int nextTurnMaxWide { get; set; }
		public int nextTurnTotalBoards { get; set; }
		public int concedeLvl { get; set; }
		public int bigMobAttack { get; set; }
		public int aggroboarder { get; set; }
	}
}
	