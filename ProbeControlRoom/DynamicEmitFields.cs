/*
VirindiHelpers.DynamicEmitFields
Copyright (C) 2017  Virindi

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace VirindiHelpers
{
	static class DynamicEmitFields
	{
		public delegate T delCreateDynamicInstanceFieldGet<T, Cls>(Cls c);
		public static delCreateDynamicInstanceFieldGet<T, Cls> CreateDynamicInstanceFieldGet<T, Cls>(string fieldname)
		{
			string newname = string.Format("__DynGet__{0}__{1}", typeof(Cls), fieldname);

			FieldInfo fi = typeof(Cls).GetField(fieldname, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fi == null)
				throw new ArgumentException ("Instance field not found.", "fieldname");
			if (typeof(T) != fi.FieldType)
				throw new ArgumentException ("Instance field is of the wrong type.", "fieldname");
			
			DynamicMethod temp = new DynamicMethod(newname, MethodAttributes.Static | MethodAttributes.Public, CallingConventions.Standard, typeof(T), new Type[] { typeof(Cls) }, MethodBase.GetCurrentMethod().DeclaringType, true);

			ILGenerator gen = temp.GetILGenerator();
			gen.Emit(OpCodes.Ldarg_0);
			gen.Emit(OpCodes.Ldfld, fi);
			gen.Emit(OpCodes.Ret);

			delCreateDynamicInstanceFieldGet<T, Cls> ret = (delCreateDynamicInstanceFieldGet<T, Cls>)temp.CreateDelegate(typeof(delCreateDynamicInstanceFieldGet<T, Cls>));

			return ret;
		}

		public delegate void delCreateDynamicInstanceFieldSet<T, Cls>(Cls c, T val);
		public static delCreateDynamicInstanceFieldSet<T, Cls> CreateDynamicInstanceFieldSet<T, Cls>(string fieldname)
		{
			string newname = string.Format("__DynSet__{0}__{1}", typeof(Cls), fieldname);

			FieldInfo fi = typeof(Cls).GetField(fieldname, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fi == null)
				throw new ArgumentException ("Instance field not found.", "fieldname");
			if (typeof(T) != fi.FieldType)
				throw new ArgumentException ("Instance field is of the wrong type.", "fieldname");
			
			DynamicMethod temp = new DynamicMethod(newname, MethodAttributes.Static | MethodAttributes.Public, CallingConventions.Standard, null, new Type[] { typeof(Cls), typeof(T) }, MethodBase.GetCurrentMethod().DeclaringType, true);

			ILGenerator gen = temp.GetILGenerator();
			gen.Emit(OpCodes.Ldarg_0);
			gen.Emit(OpCodes.Ldarg_1);
			gen.Emit(OpCodes.Stfld, fi);
			gen.Emit(OpCodes.Ret);

			delCreateDynamicInstanceFieldSet<T, Cls> ret = (delCreateDynamicInstanceFieldSet<T, Cls>)temp.CreateDelegate(typeof(delCreateDynamicInstanceFieldSet<T, Cls>));
			return ret;
		}
	}
}

