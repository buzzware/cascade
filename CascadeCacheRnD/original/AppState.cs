// using System;
// using System.Collections;
// using System.Collections.Specialized;
// using System.ComponentModel;
// using System.Linq;
// using System.Runtime.CompilerServices;
//
// namespace Cascade {
//
// 	public class Account : CascadeModel {
// 		//public event PropertyChangedEventHandler PropertyChanged;
//
// 		public string Id { get; set; }
// 		public string Name { get; set; }
// 		
// 		public override string GetResourceId() {
// 			return Id;
// 		}
// 	}
//
// /*
// Ideally :
//
// 	public class AppState : INotifyPropertyChanged {
// 	
// 		public ObservableDictionary<string, Account> Accounts = new ObservableDictionary<string, Account>(item => item.Id);
// 		
// 		public string AccountId { get; set; }
// 		
// 		[DictionaryLookup(Source = "Accounts", Selecter = "AccountId")]
// 		public Account Account { get; set; }
// 		
// 	}
//
// */
//
//
// 	public class AppState : INotifyPropertyChanged {
// 		public event PropertyChangedEventHandler PropertyChanged;
//
// 		private ModelDictionary<Account> _Accounts;
// 		public ModelDictionary<Account> Accounts {
// 			get {
// 				// lazily create this here so that we can attach listener to the initial dictionary
// 				if (_Accounts == null) {
// 					_Accounts = new ModelDictionary<Account>();
// 					_Accounts.CollectionChanged += AccountsOnCollectionChanged;
// 				}
// 				return _Accounts;
// 			}
// 			set {
// 				if (value == _Accounts)
// 					return;
// 				
// 				if (_Accounts!=null)
// 					_Accounts.CollectionChanged -= AccountsOnCollectionChanged;
// 				if (value!=null)
// 					value.CollectionChanged += AccountsOnCollectionChanged;					
// 				_Accounts = value;
// 				PropertyChanged(this,new PropertyChangedEventArgs("Accounts"));
// 			}
// 		}
//
// 		// fire Account changed if matching account in Accounts changed
// 		private void AccountsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args) {
// 			var nullishSelecter = AccountId == null;
// 			if (nullishSelecter)
// 				return;
// 			switch (args.Action) {
// 				case NotifyCollectionChangedAction.Move:
// 					break;					
// 				case NotifyCollectionChangedAction.Reset:
// 					PropertyChanged(this,new PropertyChangedEventArgs("Account"));
// 					break;
// 				case NotifyCollectionChangedAction.Remove:
// 				case NotifyCollectionChangedAction.Replace:
// 				case NotifyCollectionChangedAction.Add:
// 					// try finding matching id in old and new items
// 					var found = false;
// 					if (args.NewItems!=null)
// 						foreach (var item in args.NewItems) {
// 							ICascadeModel cm = item as ICascadeModel;
// 							if (cm == null)
// 								continue;
// 							if (cm.GetResourceId() == AccountId) {
// 								found = true;
// 								break;
// 							}
// 						}
// 					if (!found && args.OldItems!=null)
// 						foreach (var item in args.OldItems) {
// 							ICascadeModel cm = item as ICascadeModel;
// 							if (cm == null)
// 								continue;
// 							if (cm.GetResourceId() == AccountId) {
// 								found = true;
// 								break;
// 							}
// 						}
// 					if (found)
// 						PropertyChanged(this, new PropertyChangedEventArgs("Account"));
// 					break;
// 			}		
// 		}
//
// 		public string AccountId { get; set; }
//
// 		//[DictionaryLookup(Source = "Accounts", Selecter = "AccountId")]
// 		public Account Account {
// 			get {
// 				if (Accounts == null || AccountId == null || !Accounts.ContainsKey(AccountId))
// 					return null;
// 				return Accounts[AccountId];
// 			}
// 		}
// 	}
// }
