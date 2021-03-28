//GLOBAL USINGS
using System;

using static CI.ExecResult;
using static CI.Structs.ResultObject;

//BASE NAMESPACE
namespace CI
{
    public enum ExecResult
    {
        OK,

        INVALID_SCOPE,
        MISSING_OR_INVALID_VAR,

        CANNOT_DEFINE_UPSCOPE,
        CANNOT_DELETE_UPSCOPE,
        CANNOT_REDEFINE,
        CANNOT_ADD_TO_NONSTRUCT,

        INVALID_CAST,
        COMPARING_DIFFERING_TYPES,

        NON_MATCHING_DICTS,

        UNDEFINED_STRUCT,
        INCORRECT_ARGUMENT_COUNT_FOR_STRUCT,
        INCORRECT_ARGUMENT_COUNT_FOR_FUNCTION_CALL,

        CONDITIONAL_IS_NOT_BOOL,

        MISPLACED_CONTROL_FLOW_STATEMENT,

        INTERNAL_ERROR
    }

    [Flags]
    public enum ModifierFlags
    {
        NONE = 0,
        /// <summary>
        /// Cannot be set after created
        /// </summary>
        READONLY = 1,
        /// <summary>
        /// Subvariables cannot be deleted or created
        /// </summary>
        STABLE = 2,
        /// <summary>
        /// Cannot be deleted by user code
        /// Placed automatically on all function arguments
        /// and inherent variables
        /// </summary>
        UNDELETABLE = 4,
    }
    public enum FlowControlType
    {
        BREAK,
        CONTINUE
    }

    public static class HashAlgorithm
    {
        /// <summary>
        /// https://stackoverflow.com/a/1646913
        /// </summary>
        public static int Combine<T1, T2>(T1 a, T2 b)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + a.GetHashCode();
                hash = hash * 31 + b.GetHashCode();
                return hash;
            }
        }
    }

    namespace Structs
    {
        public readonly struct PrehashedString
        {
            public readonly string str;
            public readonly int hashcode;

            public PrehashedString(string str)
            {
                this.str = str;
                hashcode = str.GetHashCode();
            }

            public override int GetHashCode()
                => hashcode;
            public override bool Equals(object obj)
                => obj is PrehashedString s && s.str == str;

            public static implicit operator PrehashedString(string s) => new PrehashedString(s);
            public static bool operator ==(PrehashedString l, PrehashedString r)
                => l.Equals(r);
            public static bool operator !=(PrehashedString l, PrehashedString r)
                => !l.Equals(r);
        }
        public readonly struct AbsoluteMemoryIndex
        {
            public readonly string str;
            public readonly int scope;
            public readonly int hashcode;

            public AbsoluteMemoryIndex(string str, int scope)
            {
                this.str = str;
                this.scope = scope;

                hashcode = HashAlgorithm.Combine(str, scope);
            }

            public AbsoluteMemoryIndex(PrehashedString str, int scope)
            {
                this.str = str.str;
                this.scope = scope;

                hashcode = HashAlgorithm.Combine(str, scope);
            }

            public override int GetHashCode()
                => hashcode;

            public override bool Equals(object obj)
                => obj is AbsoluteMemoryIndex idx && str == idx.str && scope == idx.scope;
        }
        public readonly struct RelativeMemoryIndex
        {
            public readonly PrehashedString str;
            public readonly int upscope;

            public RelativeMemoryIndex(PrehashedString str, int upscope)
            {
                this.str = str;
                this.upscope = upscope;
            }

            public AbsoluteMemoryIndex ToAbsolute(int scope)
                => new AbsoluteMemoryIndex(str, scope);
        }
        public readonly struct MemoryPath
        {
            public readonly RelativeMemoryIndex memoryIndex;
            public readonly PrehashedString[] subindicies;

            public int Sublength => subindicies.Length;
            public bool SharesRoot(RelativeMemoryIndex idx)
                => idx.str == memoryIndex.str && idx.upscope == memoryIndex.upscope;

            public MemoryPath(RelativeMemoryIndex memoryIndex, PrehashedString[] subindicies)
            {
                this.memoryIndex = memoryIndex;
                this.subindicies = subindicies;
            }

            public static MemoryPath FromStr(string s, int upscope = 0)
            {
                int start = 0;
                while (s[start] == '$')
                    start++;

                string[] spl = s.Substring(start).Split('.');

                RelativeMemoryIndex mi = new RelativeMemoryIndex(spl[0], upscope);
                PrehashedString[] strs;

                if (spl.Length > 0)
                {
                    strs = new PrehashedString[spl.Length - 1];
                    for (int i = 0; i < strs.Length; i++)
                    {
                        strs[i] = spl[i + 1];
                    }
                }
                else
                {
                    strs = new PrehashedString[0];
                }

                return new MemoryPath(mi, strs);
            }
        }
        public readonly struct StructureDefinition
        {
            public readonly PrehashedString[] ids;
            public readonly ModifierFlags flags;
            public StructureDefinition(PrehashedString[] ids, ModifierFlags flags)
            {
                this.ids = ids;
                this.flags = flags;
            }
        }
        public readonly struct FunctionDefinition
        {
            public readonly PrehashedString[] argNames;
            public readonly Main.UnitGroupReturnable code;

            public FunctionDefinition(PrehashedString[] argNames, Main.UnitGroupReturnable code)
            {
                this.argNames = argNames;
                this.code = code;
            }
        }
        public readonly struct ResultObject
        {
            public readonly object obj;
            public readonly ExecResult execResult;

            public ResultObject(object obj, ExecResult execResult)
            {
                this.obj = obj;
                this.execResult = execResult;
            }

            public void Deconstruct(out object o, out ExecResult res)
            {
                o = obj;
                res = execResult;
            }

            public bool Err => execResult != OK;

            public T TryGetAs<T>(out bool err)
            {
                if (!(obj is T t) || Err)
                {
                    err = true;
                    return default;
                }

                err = false;
                return t;
            }
            public T GetAs<T>() => (T)obj;

            public static ResultObject Error(ExecResult res)
                   => new ResultObject(null, res);
            public static ResultObject Result(object obj = null)
                   => new ResultObject(obj, ExecResult.OK);
        }
    }
    namespace Tables
    {
        //USING STATEMENTS
        using Structs;
        using System;
        using System.Collections;
        using System.Collections.Generic;
        using System.Linq;
        using ITableEnumerable = System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<Structs.PrehashedString, TableEntry>>;
        using ITableEnumerator = System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<Structs.PrehashedString, TableEntry>>;
        using MemoryTableUnderlying = System.Collections.Generic.Dictionary<Structs.AbsoluteMemoryIndex, TableEntry>;
        using TableUnderlying = System.Collections.Generic.Dictionary<Structs.PrehashedString, TableEntry>;

        //TABLE DATA (CLASSES, DELEGATE)
        public delegate void AggregateFunc<T>(ref T aggregateVar, T value);
        public class TableEntry
        {
            public object obj;
            public ModifierFlags flags;
            public TableEntry(object obj, ModifierFlags flags = ModifierFlags.NONE)
            {
                this.obj = obj;
                this.flags = flags;
            }
        }
        public class MemoryTable
        {
            internal MemoryTableUnderlying underlying;

            public MemoryTable()
                => underlying = new MemoryTableUnderlying();
            public MemoryTable(MemoryTableUnderlying underlying)
                => this.underlying = underlying;

            public int Length => underlying.Count;

            public TableEntry this[AbsoluteMemoryIndex idx]
                => underlying[idx];

            public void Add(AbsoluteMemoryIndex id, TableEntry item)
            {
                underlying.Add(id, item);
            }
            public TableEntry Remove(AbsoluteMemoryIndex id)
            {
                TableEntry ret = underlying[id];
                underlying.Remove(id);
                return ret;
            }
            public bool ContainsKey(AbsoluteMemoryIndex id)
                => underlying.ContainsKey(id);

            public TableEntry EntryAt(
                MemoryPath path,
                int curScope,
                int end = -1,
                ModifierFlags anyInvalidFlags = ModifierFlags.NONE,
                ModifierFlags leadingInvalidFlags = ModifierFlags.NONE,
                ModifierFlags finalInvalidFlags = ModifierFlags.NONE)
            {
                end = (end == -1 ? path.Sublength - 1 : end);
                TableEntry uppermost = null;

                if (path.memoryIndex.upscope == -1)
                {
                    AbsoluteMemoryIndex absIdx = path.memoryIndex.ToAbsolute(0);
                    if (!underlying.ContainsKey(absIdx))
                        return null;
                    else
                        uppermost = underlying[absIdx];
                }
                else
                {
                    int cSeen = 0;

                    for (int i = curScope; i >= 0; i--)
                    {
                        AbsoluteMemoryIndex idx = path.memoryIndex.ToAbsolute(i);
                        if (!underlying.ContainsKey(idx)) continue;

                        cSeen++;
                        if (cSeen > path.memoryIndex.upscope)
                        {
                            uppermost = underlying[idx];
                            break;
                        }
                    }

                    if (uppermost == null)
                        return null;
                }

                if (path.Sublength == 0)
                    return uppermost;

                if (!(uppermost.obj is Table cTable))
                    return null;

                for (int i = 0; i < end; i++)
                {
                    if (!cTable.underlying.ContainsKey(path.subindicies[i]))
                        return null;

                    cTable = cTable[path.subindicies[i]].obj as Table;
                    if (cTable == null)
                        return null;

                    if ((cTable.flags & anyInvalidFlags & leadingInvalidFlags) != 0)
                        return null;
                }

                if (!cTable.underlying.ContainsKey(path.subindicies[end]))
                    return null;

                if ((cTable.flags & anyInvalidFlags & finalInvalidFlags) != 0)
                    return null;

                return cTable.underlying[path.subindicies[end]];
            }
        }
        public class Table : ITableEnumerable
        {
            public ModifierFlags flags;
            internal TableUnderlying underlying;

            public int Length => underlying.Count;

            public Table(TableUnderlying underlying, ModifierFlags flags = ModifierFlags.NONE)
            {
                this.underlying = underlying;
                this.flags = flags;
            }
            public Table() : this(new TableUnderlying()) {; }

            public override bool Equals(object obj)
                => obj is Table str && Equals(str);
            public bool Equals(Table other)
                => underlying.SequenceEqual(other.underlying);
            public override int GetHashCode()
                => base.GetHashCode();

            public void Add(PrehashedString id, TableEntry item)
                => underlying.Add(id, item);
            public TableEntry Remove(PrehashedString id)
            {
                TableEntry ret = underlying[id];
                underlying.Remove(id);
                return ret;
            }
            public bool ContainsKey(PrehashedString id)
                => underlying.ContainsKey(id);

            public ITableEnumerator GetEnumerator()
                => underlying.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public TableEntry this[PrehashedString id]
            {
                get => underlying[id];
                set => underlying[id] = value;
            }

            public static bool Add(ref Table s, PrehashedString id, TableEntry item)
            {
                s.Add(id, item);
                if (!(item.obj is float) && s is VectorTable)
                {
                    s = new Table(s.underlying);
                }
                return true;
            }

            public override string ToString()
            {
                string ret = "";
                int cnt = 0;
                foreach (var r in this)
                {
                    if (cnt > 0) ret += ", ";
                    ret += $"{{{r.Key}: ";
                    if (r.Value.obj is Table tbl)
                    {
                        ret += $"{{{tbl}}}";
                    }
                    else
                    {
                        ret += r.Value.obj;
                    }
                    ret += "}";

                    if (++cnt % 3 == 0)
                    {
                        ret += "\n";
                    }
                }
                return ret;
            }
        }
        public class VectorTable : Table
        {
            public VectorTable(Dictionary<PrehashedString, float> dict)
                => underlying = dict.ToDictionary(k => k.Key, k => new TableEntry(k.Value));
            public VectorTable() : base() {; }

            public void Add(PrehashedString id, float v) => Add(id, new TableEntry(v));

            public VectorTable ForEach(Func<float, float> func)
            {
                VectorTable newStruct = new VectorTable();
                foreach (var kvp in underlying)
                {
                    newStruct.Add(
                        kvp.Key,
                        new TableEntry(func((float)kvp.Value.obj))
                    );
                }
                return newStruct;
            }
            public float Aggregate(AggregateFunc<float> func, float agg = default)
            {
                foreach (var v in underlying.Values)
                {
                    func(ref agg, (float)v.obj);
                }
                return agg;
            }
            public ResultObject Zip(VectorTable other, Func<float, float, float> func)
            {
                if (Length != other.Length) return Error(NON_MATCHING_DICTS);

                VectorTable newStruct = new VectorTable();
                foreach (var kvp in underlying)
                {
                    if (!other.underlying.TryGetValue(kvp.Key, out var oVal))
                        return Error(INVALID_CAST);

                    newStruct.Add(
                        kvp.Key,
                        new TableEntry(func((float)kvp.Value.obj, (float)oVal.obj))
                    );
                }
                return Result(newStruct);
            }

            public float GetMin() => Aggregate((ref float agg, float n) => agg = n < agg ? n : agg, float.MaxValue);
            public float GetMax() => Aggregate((ref float agg, float n) => agg = n > agg ? n : agg, float.MinValue);
            public float GetSum() => Aggregate((ref float agg, float n) => agg += n);
            public float GetAverage() => GetSum() / Length;
            public float GetMagnitudeSq() => Aggregate((ref float agg, float n) => agg += n * n);
            public float GetMagnitude() => UnityEngine.Mathf.Sqrt(GetMagnitudeSq());
        }
    }
    namespace Main
    {
        //USING STATEMENTS
        using Structs;
        using Tables;
        using System.Collections.Generic;

        //SCRIPT MANAGER
        public class ScriptManager
        {
            private readonly MemoryTable memory;
            public int currentScope = 0;
            public List<List<AbsoluteMemoryIndex>> scopes;

            public ScriptManager()
            {
                memory = new MemoryTable();
                scopes = new List<List<AbsoluteMemoryIndex>>();
            }

            public ExecResult Execute(IUnit unit)
            {
                var r = unit.Evaluate(this);
                return r.execResult;
            }

            public ResultObject this[string idx]
            {
                get => GetVariable(MemoryPath.FromStr(idx));
                set => SetVariable(MemoryPath.FromStr(idx), value);
            }

            #region (public methods) Scope Management
            public void EnterScope()
                => scopes.Add(new List<AbsoluteMemoryIndex>());
            public void ExitScope()
            {
                for (int i = 0; i < scopes[scopes.Count - 1].Count; i++)
                {
                    memory.Remove(scopes[scopes.Count - 1][i]);
                }
                scopes.RemoveAt(scopes.Count - 1);
            }
            #endregion

            #region (public methods) Variable Management
            public ResultObject GetVariable(MemoryPath path)
            {
                TableEntry cEntry = memory.EntryAt(path, currentScope);
                if (cEntry == null)
                    return Error(MISSING_OR_INVALID_VAR);

                return Result(cEntry.obj);
            }

            public ResultObject SetVariable(MemoryPath path, object value)
            {
                TableEntry cEntry = memory.EntryAt(path, currentScope);
                if (cEntry == null)
                    return Error(MISSING_OR_INVALID_VAR);

                cEntry.obj = value;
                return Result();
            }

            public ResultObject DefVariable(MemoryPath path, object value, ModifierFlags flags = ModifierFlags.NONE)
            {
                if (path.memoryIndex.upscope != 0)
                    return Error(CANNOT_DEFINE_UPSCOPE);

                if (path.Sublength == 0)
                {
                    AbsoluteMemoryIndex idx = path.memoryIndex.ToAbsolute(currentScope);
                    if (memory.ContainsKey(idx))
                        return Error(CANNOT_REDEFINE);
                    memory.Add(idx, new TableEntry(value));
                    if (scopes.Count > 0)
                        scopes[scopes.Count - 1].Add(idx);
                    return Result();
                }

                TableEntry cEntry =
                    memory.EntryAt(
                        path, currentScope, path.Sublength - 2, leadingInvalidFlags: ModifierFlags.STABLE, finalInvalidFlags: ModifierFlags.UNDELETABLE
                    );

                if (cEntry == null)
                    return Error(MISSING_OR_INVALID_VAR);
                if (!(cEntry.obj is Table cStruct))
                    return Error(CANNOT_ADD_TO_NONSTRUCT);
                if (cStruct.ContainsKey(path.subindicies[path.Sublength - 1]))
                    return Error(CANNOT_REDEFINE);

                cStruct.Add(path.subindicies[path.Sublength - 1], new TableEntry(value, flags));

                return Result();
            }

            public ResultObject DelVariable(MemoryPath path)
            {
                //?????!?!?!? might not be required but im an asswipe so idfk
                if (path.memoryIndex.upscope != 0)
                    return Error(CANNOT_DELETE_UPSCOPE);

                if (path.Sublength == 0 && scopes.Count > 0)
                    scopes[scopes.Count - 1].Remove(path.memoryIndex.ToAbsolute(currentScope));

                TableEntry cEntry =
                    memory.EntryAt(
                        path, path.Sublength - 2, leadingInvalidFlags: ModifierFlags.STABLE, finalInvalidFlags: ModifierFlags.UNDELETABLE
                    );

                if (cEntry == null)
                    return Error(MISSING_OR_INVALID_VAR);

                if (!(cEntry.obj is Table cStruct))
                    return Error(MISSING_OR_INVALID_VAR);

                if (!cStruct.ContainsKey(path.subindicies[path.Sublength - 1]))
                    return Error(MISSING_OR_INVALID_VAR);

                TableEntry entry = cStruct.Remove(path.subindicies[path.Sublength - 1]);

                return Result(entry);
            }

            public ResultObject VariableExists(MemoryPath path)
            {
                TableEntry cEntry = memory.EntryAt(path, currentScope);
                return Result(cEntry != null);
            }
            #endregion
        }

        //UNIT BASE IMPLEMENTATION
        #region (public interface) IUnit
        public interface IUnit
        {
            public ResultObject Evaluate(ScriptManager manager);
        }
        #endregion
        #region (public, abstract) Basic Unit Types
        public abstract class UnaryUnit<T> : IUnit
        {
            public readonly T val;

            protected UnaryUnit(T val)
            {
                this.val = val;
            }

            public abstract ResultObject Evaluate(ScriptManager manager);
        }
        public abstract class BinaryUnit<T> : IUnit
        {
            public readonly T left;
            public readonly T right;

            protected BinaryUnit(T left, T right)
            {
                this.left = left;
                this.right = right;
            }

            public abstract ResultObject Evaluate(ScriptManager manager);
        }
        public abstract class PolynaryUnit<T> : IUnit
        {
            public readonly T[] inputs;

            protected PolynaryUnit(T[] inputs)
            {
                this.inputs = inputs;
            }

            public abstract ResultObject Evaluate(ScriptManager manager);
        }
        #endregion
        #region (public, abstract) Other Unit Types
        public abstract class PreparseUnaryUnit : UnaryUnit<IUnit>
        {
            public PreparseUnaryUnit(IUnit val) : base(val) {; }

            public override ResultObject Evaluate(ScriptManager manager)
            {
                ResultObject valObj = val.Evaluate(manager);
                if (valObj.Err) return valObj;

                return EvaluateObj(manager, valObj.obj);
            }

            protected abstract ResultObject EvaluateObj(ScriptManager manager, object v);
        }
        public abstract class PreparseUnaryUnit<T1> : PreparseUnaryUnit
        {
            public PreparseUnaryUnit(IUnit val) : base(val) {; }

            protected override ResultObject EvaluateObj(ScriptManager manager, object v)
            {
                if (v is T1 tVal) return EvaluateType1(manager, tVal);
                return Error(INVALID_CAST);
            }
            protected virtual ResultObject EvaluateType1(ScriptManager manager, T1 v) => Error(INVALID_CAST);
        }
        public abstract class PreparseUnaryUnit<T1, T2> : PreparseUnaryUnit<T1>
        {
            public PreparseUnaryUnit(IUnit val) : base(val) {; }

            protected override ResultObject EvaluateObj(ScriptManager manager, object v)
            {
                if (v is T1 t1Val) return EvaluateType1(manager, t1Val);
                if (v is T2 t2Val) return EvaluateType2(manager, t2Val);
                return Error(INVALID_CAST);
            }
            protected virtual ResultObject EvaluateType2(ScriptManager manager, T2 v) => Error(INVALID_CAST);
        }
        public abstract class PreparseBinaryUnit : BinaryUnit<IUnit>
        {
            public PreparseBinaryUnit(IUnit left, IUnit right) : base(left, right) {; }

            public override ResultObject Evaluate(ScriptManager manager)
            {
                ResultObject leftObj = left.Evaluate(manager);
                if (leftObj.Err) return leftObj;

                ResultObject rightObj = right.Evaluate(manager);
                if (rightObj.Err) return rightObj;

                return EvaluateObjs(manager, leftObj.obj, rightObj.obj);
            }

            protected abstract ResultObject EvaluateObjs(ScriptManager manager, object l, object r);
        }
        public abstract class PreparseBinaryUnit<T1> : PreparseBinaryUnit
        {
            public PreparseBinaryUnit(IUnit left, IUnit right) : base(left, right) {; }

            protected override ResultObject EvaluateObjs(ScriptManager manager, object l, object r)
            {
                if (!(l is T1 t1l)) return Error(INVALID_CAST);
                if (!(r is T1 t1r)) return Error(INVALID_CAST);
                return EvaluateType1Type1(manager, t1l, t1r);
            }
            protected virtual ResultObject EvaluateType1Type1(ScriptManager manager, T1 l, T1 r) => Error(INVALID_CAST);
        }
        public abstract class PreparseBinaryUnit<T1, T2> : PreparseBinaryUnit<T1>
        {
            public PreparseBinaryUnit(IUnit left, IUnit right) : base(left, right) {; }

            protected override ResultObject EvaluateObjs(ScriptManager manager, object l, object r)
            {
                if (l is T1 t1l)
                {
                    if (r is T1 t1r)
                    {
                        return EvaluateType1Type1(manager, t1l, t1r);
                    }
                    else if (r is T2 t2r)
                    {
                        return EvaluateType1Type2(manager, t1l, t2r);
                    }
                }
                else if (l is T2 t2l)
                {
                    if (r is T1 t1r)
                    {
                        return EvaluateType2Type1(manager, t2l, t1r);
                    }
                    else if (r is T2 t2r)
                    {
                        return EvaluateType2Type2(manager, t2l, t2r);
                    }
                }

                return Error(INVALID_CAST);
            }
            protected virtual ResultObject EvaluateType1Type2(ScriptManager manager, T1 l, T2 r) => Error(INVALID_CAST);
            protected virtual ResultObject EvaluateType2Type1(ScriptManager manager, T2 l, T1 r) => Error(INVALID_CAST);
            protected virtual ResultObject EvaluateType2Type2(ScriptManager manager, T2 l, T2 r) => Error(INVALID_CAST);
        }
        public abstract class PreparseBinaryUnit<T1, T2, T3> : PreparseBinaryUnit<T1, T2>
        {
            public PreparseBinaryUnit(IUnit left, IUnit right) : base(left, right) {; }

            protected override ResultObject EvaluateObjs(ScriptManager manager, object l, object r)
            {
                if (l is T1 t1l)
                {
                    if (r is T1 t1r)
                    {
                        return EvaluateType1Type1(manager, t1l, t1r);
                    }
                    else if (r is T2 t2r)
                    {
                        return EvaluateType1Type2(manager, t1l, t2r);
                    }
                    else if (r is T3 t3r)
                    {
                        return EvaluateType1Type3(manager, t1l, t3r);
                    }
                }
                else if (l is T2 t2l)
                {
                    if (r is T1 t1r)
                    {
                        return EvaluateType2Type1(manager, t2l, t1r);
                    }
                    else if (r is T2 t2r)
                    {
                        return EvaluateType2Type2(manager, t2l, t2r);
                    }
                    else if (r is T3 t3r)
                    {
                        return EvaluateType2Type3(manager, t2l, t3r);
                    }
                }
                else if (l is T3 t3l)
                {
                    if (r is T1 t1r)
                    {
                        return EvaluateType3Type1(manager, t3l, t1r);
                    }
                    else if (r is T2 t2r)
                    {
                        return EvaluateType3Type2(manager, t3l, t2r);
                    }
                    else if (r is T3 t3r)
                    {
                        return EvaluateType3Type3(manager, t3l, t3r);
                    }
                }

                return Error(INVALID_CAST);
            }
            protected virtual ResultObject EvaluateType1Type3(ScriptManager manager, T1 l, T3 r) => Error(INVALID_CAST);
            protected virtual ResultObject EvaluateType2Type3(ScriptManager manager, T2 l, T3 r) => Error(INVALID_CAST);
            protected virtual ResultObject EvaluateType3Type3(ScriptManager manager, T3 l, T3 r) => Error(INVALID_CAST);
            protected virtual ResultObject EvaluateType3Type2(ScriptManager manager, T3 l, T2 r) => Error(INVALID_CAST);
            protected virtual ResultObject EvaluateType3Type1(ScriptManager manager, T3 l, T1 r) => Error(INVALID_CAST);
        }
        #endregion

        //BASIC UNITS
        #region (public, abstract? classes) Basic Units
        public abstract class FlowControlUnit : UnaryUnit<FlowControlType>
        {
            public FlowControlUnit(FlowControlType val) : base(val) {; }
        }
        public class ReturnUnit : UnaryUnit<IUnit>
        {
            public ReturnUnit(IUnit val) : base(val) {; }
            public override ResultObject Evaluate(ScriptManager manager)
                => val.Evaluate(manager);
        }
        public class UnitGroup : PolynaryUnit<IUnit>
        {
            public UnitGroup(IUnit[] units) : base(units) {; }

            public override ResultObject Evaluate(ScriptManager manager)
            {
                ResultObject obj;

                for (int i = 0; i < inputs.Length; i++)
                {
                    if (inputs[i] is FlowControlUnit || inputs[i] is ReturnUnit)
                        Error(MISPLACED_CONTROL_FLOW_STATEMENT);

                    obj = inputs[i].Evaluate(manager);
                    if (obj.Err) return obj;
                }

                return Result();
            }
        }
        public class UnitGroupLoopable : PolynaryUnit<IUnit>
        {
            public UnitGroupLoopable(IUnit[] units) : base(units) {; }

            //Returns:
            // - null for natural completion
            // - FlowControlType for break/continue
            public override ResultObject Evaluate(ScriptManager manager)
            {
                ResultObject obj;

                for (int i = 0; i < inputs.Length; i++)
                {
                    if (inputs[i] is ReturnUnit)
                        Error(MISPLACED_CONTROL_FLOW_STATEMENT);

                    if (inputs[i] is FlowControlUnit fcu)
                        return Result(fcu.val);

                    obj = inputs[i].Evaluate(manager);
                    if (obj.Err) return obj;
                }

                return Result();
            }
        }
        public class UnitGroupReturnable : PolynaryUnit<IUnit>
        {
            public UnitGroupReturnable(IUnit[] units) : base(units) {; }

            //Default return = null
            public override ResultObject Evaluate(ScriptManager manager)
            {
                ResultObject obj;

                for (int i = 0; i < inputs.Length; i++)
                {
                    if (inputs[i] is ReturnUnit ret)
                        return ret.Evaluate(manager);

                    obj = inputs[i].Evaluate(manager);
                    if (obj.Err) return obj;

                }

                return Result();
            }
        }
        public class RawValue : UnaryUnit<object>
        {
            public RawValue(object obj) : base(obj) {; }

            public override ResultObject Evaluate(ScriptManager manager)
                => Result(val);
        }
        #endregion

        //STRUCTURE MANAGEMENT
        #region (public classes) Table Management
        public class AnonTableBuild : PolynaryUnit<IUnit>
        {
            public readonly PrehashedString[] subids;
            public readonly ModifierFlags flags;

            public AnonTableBuild(IUnit[] inputs, PrehashedString[] subids, ModifierFlags flags) : base(inputs)
            {
                this.subids = subids;
                this.flags = flags;
            }

            public static ResultObject Get(ScriptManager manager, PrehashedString[] ids, ModifierFlags flags, IUnit[] inputs)
            {
                Table ret = new VectorTable();
                ret.flags = flags;

                for (int i = 0; i < ids.Length; i++)
                {
                    ResultObject res = inputs[i].Evaluate(manager);
                    if (res.Err) return res;

                    Table.Add(ref ret, ids[i], new TableEntry(res.obj));
                }

                return Result(ret);
            }

            public override ResultObject Evaluate(ScriptManager manager) => Get(manager, subids, flags, inputs);
        }
        public class StructBuild : PolynaryUnit<IUnit>
        {
            public readonly MemoryPath path;
            public StructBuild(IUnit[] inputs, MemoryPath path) : base(inputs)
            {
                this.path = path;
            }

            public override ResultObject Evaluate(ScriptManager manager)
            {
                ResultObject sdef = manager.GetVariable(path);
                if (sdef.Err)
                    return sdef;

                if (!(sdef.obj is StructureDefinition structDef))
                    return Error(UNDEFINED_STRUCT);

                if (inputs.Length != structDef.ids.Length)
                    return Error(INCORRECT_ARGUMENT_COUNT_FOR_STRUCT);

                return AnonTableBuild.Get(manager, structDef.ids, structDef.flags, inputs);
            }
        }
        #endregion

        //IDENTIFIER MANAGEMENT UNITS
        #region (public classes) Identifier Management Units
        public class IdentifierRead : UnaryUnit<MemoryPath>
        {
            public IdentifierRead(MemoryPath val) : base(val) {; }

            public override ResultObject Evaluate(ScriptManager manager)
            {
                return manager.GetVariable(val);
            }
        }
        public class IdentifierWrite : PreparseUnaryUnit
        {
            public readonly MemoryPath input;

            public IdentifierWrite(MemoryPath input, IUnit val) : base(val)
            {
                this.input = input;
            }

            protected override ResultObject EvaluateObj(ScriptManager manager, object v)
            {
                return manager.SetVariable(input, v);
            }
        }
        public class IdentifierDef : PreparseUnaryUnit
        {
            public readonly MemoryPath input;
            public readonly ModifierFlags flags;

            public IdentifierDef(MemoryPath input, ModifierFlags flags, IUnit val) : base(val)
            {
                this.input = input;
                this.flags = flags;
            }

            protected override ResultObject EvaluateObj(ScriptManager manager, object v)
            {
                return manager.DefVariable(input, v);
            }
        }
        public class IdentifierDel : UnaryUnit<MemoryPath>
        {
            public IdentifierDel(MemoryPath input) : base(input) {; }

            public override ResultObject Evaluate(ScriptManager manager)
            {
                return manager.DelVariable(val);
            }
        }
        public class IdentifierExists : UnaryUnit<MemoryPath>
        {
            public IdentifierExists(MemoryPath input) : base(input) {; }

            public override ResultObject Evaluate(ScriptManager manager)
            {
                return manager.VariableExists(val);
            }
        }
        #endregion

        //BOOLEAN OPS
        #region (public, abstract? classes) Boolean Operation Units
        public class BoolEquals : PreparseBinaryUnit
        {
            public BoolEquals(IUnit left, IUnit right) : base(left, right) {; }

            protected override ResultObject EvaluateObjs(ScriptManager manager, object l, object r)
            {
                return Result(l.Equals(r));
            }
        }
        public abstract class BoolComparable : PreparseBinaryUnit<IComparable>
        {
            public BoolComparable(IUnit left, IUnit right) : base(left, right) {; }
            protected override ResultObject EvaluateObjs(ScriptManager manager, object l, object r)
            {
                if (l.GetType() != r.GetType()) return Error(COMPARING_DIFFERING_TYPES);
                return Result(Compare(((IComparable)l).CompareTo(r)));
            }

            public abstract bool Compare(int cmp);
        }
        public class BoolGreater : BoolComparable
        {
            public BoolGreater(IUnit left, IUnit right) : base(left, right) {; }
            public override bool Compare(int cmp) => cmp > 0;
        }
        public class BoolGreaterEquals : BoolComparable
        {
            public BoolGreaterEquals(IUnit left, IUnit right) : base(left, right) {; }
            public override bool Compare(int cmp) => cmp >= 0;
        }
        public class BoolLess : BoolComparable
        {
            public BoolLess(IUnit left, IUnit right) : base(left, right) {; }
            public override bool Compare(int cmp) => cmp < 0;
        }
        public class BoolLessEqual : BoolComparable
        {
            public BoolLessEqual(IUnit left, IUnit right) : base(left, right) {; }
            public override bool Compare(int cmp) => cmp <= 0;
        }
        public class BoolAndOperator : PreparseBinaryUnit<bool>
        {
            public BoolAndOperator(IUnit left, IUnit right) : base(left, right) {; }
            protected override ResultObject EvaluateType1Type1(ScriptManager manager, bool l, bool r)
                => Result(l && r);
        }
        public class BoolOrOperator : PreparseBinaryUnit<bool>
        {
            public BoolOrOperator(IUnit left, IUnit right) : base(left, right) {; }
            protected override ResultObject EvaluateType1Type1(ScriptManager manager, bool l, bool r)
                => Result(l || r);
        }
        public class BoolNotOperator : PreparseUnaryUnit<bool>
        {
            public BoolNotOperator(IUnit val) : base(val) {; }
            protected override ResultObject EvaluateType1(ScriptManager manager, bool v)
                => Result(!v);
        }
        #endregion

        //BASE MATH OPERATIONS
        #region (public, abstract) Base Math Operators
        public abstract class MathUnaryOperator : PreparseUnaryUnit<float, VectorTable>
        {
            public MathUnaryOperator(IUnit val) : base(val) {; }

            public abstract float Op(float a);

            protected override ResultObject EvaluateType1(ScriptManager manager, float v)
                => Result(Op(v));
            protected override ResultObject EvaluateType2(ScriptManager manager, VectorTable v)
                => Result(v.ForEach(Op));
        }
        public abstract class MathBinaryOperator : PreparseBinaryUnit<float, VectorTable>
        {
            public MathBinaryOperator(IUnit left, IUnit right) : base(left, right) {; }

            public abstract float Op(float a, float b);

            protected override ResultObject EvaluateType1Type1(ScriptManager manager, float l, float r)
                => Result(Op(l, r));
            protected override ResultObject EvaluateType1Type2(ScriptManager manager, float l, VectorTable r)
                => Result(r.ForEach((float a) => Op(l, a)));
            protected override ResultObject EvaluateType2Type1(ScriptManager manager, VectorTable l, float r)
                => Result(l.ForEach((float a) => Op(a, r)));
            protected override ResultObject EvaluateType2Type2(ScriptManager manager, VectorTable l, VectorTable r)
                => l.Zip(r, Op);
        }
        #endregion

        //MATH OPERATORS
        #region (public classes) Math Operators
        public class AddOperator : PreparseBinaryUnit<float, VectorTable, string>
        {
            public AddOperator(IUnit left, IUnit right) : base(left, right) {; }
            protected override ResultObject EvaluateType1Type1(ScriptManager manager, float l, float r)
                => Result(l + r);
            protected override ResultObject EvaluateType1Type2(ScriptManager manager, float l, VectorTable r)
                => Result(r.ForEach((float a) => l + a));
            protected override ResultObject EvaluateType2Type1(ScriptManager manager, VectorTable l, float r)
                => Result(l.ForEach((float a) => a + r));
            protected override ResultObject EvaluateType2Type2(ScriptManager manager, VectorTable l, VectorTable r)
                => Result(l.Zip(r, (float a, float b) => a + b));
            protected override ResultObject EvaluateType3Type3(ScriptManager manager, string l, string r)
                => Result(l + r);
        }
        public class SubtractOperator : MathBinaryOperator
        {
            public SubtractOperator(IUnit left, IUnit right) : base(left, right) { }
            public override float Op(float a, float b) => a - b;
        }
        public class MultiplyOperator : MathBinaryOperator
        {
            public MultiplyOperator(IUnit left, IUnit right) : base(left, right) { }
            public override float Op(float a, float b) => a * b;
        }
        public class DivideOperator : MathBinaryOperator
        {
            public DivideOperator(IUnit left, IUnit right) : base(left, right) { }
            public override float Op(float a, float b) => a / b;
        }
        public class ModuloOperator : MathBinaryOperator
        {
            public ModuloOperator(IUnit left, IUnit right) : base(left, right) { }
            public override float Op(float a, float b) => a % b;
        }
        public class PowOperator : MathBinaryOperator
        {
            public PowOperator(IUnit left, IUnit right) : base(left, right) { }
            public override float Op(float a, float b) => UnityEngine.Mathf.Pow(a, b);
        }
        public class NegationOperator : MathUnaryOperator
        {
            public NegationOperator(IUnit val) : base(val) { }
            public override float Op(float a) => -a;
        }
        public class AbsOperator : MathUnaryOperator
        {
            public AbsOperator(IUnit val) : base(val) { }
            public override float Op(float a) => Math.Abs(a);
            protected override ResultObject EvaluateType2(ScriptManager manager, VectorTable v)
                => Result(v.GetMagnitude());
        }
        #endregion

        //CONTROLS
        #region (public, abstract? classes) Conditional Units
        public abstract class ConditionalUnit : UnaryUnit<IUnit>
        {
            public ConditionalUnit(IUnit cond) : base(cond) {; }

            protected ResultObject EvaluateCondition(ScriptManager manager, out bool b)
            {
                b = false;

                ResultObject condition = val.Evaluate(manager);
                if (condition.Err) return condition;

                if (!(condition.obj is bool b1))
                    return Error(CONDITIONAL_IS_NOT_BOOL);

                b = b1;

                return condition;
            }
        }
        public class IfBlock : ConditionalUnit
        {
            public readonly UnitGroup grp;
            public IfBlock(IUnit cond, UnitGroup grp) : base(cond)
            {
                this.grp = grp;
            }
            public override ResultObject Evaluate(ScriptManager manager)
            {
                ResultObject condRes = EvaluateCondition(manager, out bool condBool);
                if (condRes.Err)
                    return condRes;

                manager.EnterScope();

                if (condBool)
                {
                    return grp.Evaluate(manager);
                }

                manager.ExitScope();
                return Result();
            }
        }
        public class TernaryBlock : ConditionalUnit
        {
            public readonly IUnit onTrue, onFalse;
            public TernaryBlock(IUnit cond, IUnit onTrue, IUnit onFalse) : base(cond)
            {
                this.onTrue = onTrue;
                this.onFalse = onFalse;
            }
            public override ResultObject Evaluate(ScriptManager manager)
            {
                ResultObject condRes = EvaluateCondition(manager, out bool condBool);
                if (condRes.Err)
                    return condRes;

                if (condBool) return onTrue.Evaluate(manager);
                else return onFalse.Evaluate(manager);
            }
        }
        public class WhileBlock : ConditionalUnit
        {
            public readonly UnitGroupLoopable grp;
            public WhileBlock(IUnit cond, UnitGroupLoopable grp) : base(cond)
            {
                this.grp = grp;
            }
            public override ResultObject Evaluate(ScriptManager manager)
            {
                ResultObject condRes = EvaluateCondition(manager, out bool condBool);
                if (condRes.Err)
                    return condRes;

                ResultObject res;

                manager.EnterScope();

                while (condBool)
                {
                    condRes = EvaluateCondition(manager, out condBool);
                    if (condRes.Err)
                        return condRes;

                    res = grp.Evaluate(manager);
                    if (res.obj is bool resb && resb)
                    {
                        break;
                    }
                }

                manager.ExitScope();
                return Result();
            }
        }
        #endregion

        //FUNCTIONS
        #region (public classes) Function Management
        public class FunctionCall : PreparseUnaryUnit<FunctionDefinition>
        {
            public readonly IUnit[] args;
            public FunctionCall(IUnit val, IUnit[] args) : base(val)
            {
                this.args = args;
            }
            protected override ResultObject EvaluateType1(ScriptManager manager, FunctionDefinition v)
            {
                if (v.argNames.Length != args.Length)
                    return Error(INCORRECT_ARGUMENT_COUNT_FOR_FUNCTION_CALL);

                manager.EnterScope();

                ResultObject res;
                for (int i = 0; i < args.Length; i++)
                {
                    res = args[i].Evaluate(manager);
                    if (res.Err)
                        return res;

                    manager.DefVariable(
                        new MemoryPath(
                            new RelativeMemoryIndex(v.argNames[i], 0),
                            new PrehashedString[0]
                        ),
                        res.obj,
                        ModifierFlags.UNDELETABLE | ModifierFlags.STABLE
                    );
                }

                res = v.code.Evaluate(manager);
                if (res.Err)
                    return res;

                manager.ExitScope();

                return res;

            }
        }
        #endregion
    }
}