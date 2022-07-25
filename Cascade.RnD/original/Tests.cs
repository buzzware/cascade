// using System;
// using System.ComponentModel;
// using System.IO;
// using System.Linq;
// using Cascade;
// using NUnit.Framework;
//
// namespace CacheTests
// {
// 	public class Tests
// 	{
// 		
// 		[Test]
// 		public void AppStateAccountTest() {
// 			var appState = new AppState();
// 			var acct = new Account()
// 			{
// 				Id = "1",
// 				Name = "Fred"
// 			};
// 			bool eventFired = false;
// 			Account eventAccount = null;
// 			string eventAction = null;
// 			appState.Accounts.CollectionChanged += (sender, args) => {
// 				eventAction = args.Action.ToString();
// 			};
// 			appState.PropertyChanged += (sender, e) => {
// 				eventFired = true;
// 				eventAccount = (sender as AppState).Account;
// 			};
// 			Assert.That(eventFired,Is.False);
// 			Assert.That(appState.Account,Is.Null);
// 			appState.Accounts["1"] = acct;
// 			Assert.That(eventAction,Is.EqualTo("Add"));
// 			Assert.That(eventFired,Is.False);
// 			Assert.That(appState.Account,Is.Null);
// 			appState.AccountId = "1";
// 			Assert.That(eventFired,Is.True);
// 			Assert.That(eventAccount,Is.EqualTo(acct));
// 			Assert.That(appState.Account,Is.EqualTo(acct));
//
// 			eventFired = false;
// 			appState.AccountId = "2";
// 			Assert.That(eventFired,Is.True);
// 			Assert.That(eventAccount,Is.Null);
// 			Assert.That(appState.Account,Is.Null);
// 			
// 			eventFired = false;
// 			appState.AccountId = "1";
// 			Assert.That(eventFired,Is.True);
// 			Assert.That(eventAccount,Is.EqualTo(acct));
// 			Assert.That(appState.Account,Is.EqualTo(acct));
// 			
// 			eventFired = false;
// 			eventAction = null;
// 			appState.Accounts.Clear();	// test NotifyCollectionChangedAction.Reset
// 			Assert.That(eventAction,Is.EqualTo("Reset"));
// 			Assert.That(eventFired,Is.True);
// 			Assert.That(eventAccount,Is.Null);
// 			Assert.That(appState.Account, Is.Null);
// 			
// 			// restore
// 			eventFired = false;
// 			appState.Accounts["1"] = acct;	
// 			//Assert.That(eventFired,Is.True);
// 			Assert.That(eventAccount,Is.EqualTo(acct));
// 			Assert.That(appState.Account,Is.EqualTo(acct));
//
// 			eventFired = false;
// 			eventAction = null;
// 			appState.Accounts["1"] = null;
// 			Assert.That(eventAction,Is.EqualTo("Replace"));
// 			Assert.That(eventFired,Is.True);		// test replace
// 			Assert.That(eventAccount,Is.Null);
// 			Assert.That(appState.Account, Is.Null);
// 			
// 			// restore
// 			eventFired = false;
// 			appState.Accounts["1"] = acct;	
// 			Assert.That(eventFired,Is.True);
// 			Assert.That(eventAccount,Is.EqualTo(acct));
// 			Assert.That(appState.Account,Is.EqualTo(acct));
// 			
// 			eventFired = false;
// 			eventAction = null;
// 			appState.Accounts.Remove(acct);
// 			Assert.That(eventAction,Is.EqualTo("Remove"));
// 			Assert.That(eventFired,Is.True);		// test remove
// 			Assert.That(eventAccount,Is.Null);
// 			Assert.That(appState.Account, Is.Null);
// 		}
// 	}
// }
