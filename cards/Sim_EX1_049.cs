using System;
using System.Collections.Generic;
using System.Text;

namespace HREngine.Bots
{
	class Sim_EX1_049 : SimTemplate //youthfulbrewmaster
	{

//    kampfschrei:/ lasst einen befreundeten diener vom schlachtfeld auf eure hand zurückkehren.
		public override void getBattlecryEffect(Playfield p, Minion own, Minion target, int choice)
		{
            p.minionReturnToHand(target, target.own, 0);
		}


	}
}