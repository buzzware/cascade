﻿using System.Collections.Generic;
using Cascade;
using SQLite;

namespace Cascade {
	public class Parent : CascadeModel {
		
		[Cascade.CascadeId]
		[SQLite.PrimaryKey, SQLite.AutoIncrement]
		public long id { get; set; }
		
		[Cascade.HasMany(foreignIdProperty: "parentId")]
		[SQLite.Ignore]
		public IEnumerable<Child>? Children { get; set; }
		
		public string? colour { get; set; }
		public string? Size { get; set; }
		public long updatedAtMs { get; set; }

		public override object CascadeId() {
			return id;
		}

		public Parent() {
		}
		
		public Parent withChanges(
			string? colour = null,
			string? size = null,
			long? updatedAtMs = null
		) {
			var result = new Parent() {id = this.id};
			result.colour = colour ?? this.colour;
			result.Size = size ?? this.Size;
			result.updatedAtMs = updatedAtMs ?? this.updatedAtMs;
			return result;
		}
	}
}