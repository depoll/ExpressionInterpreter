using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using ExpressionInterpreter;
using NUnit.Framework;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Reflection.Emit;
using System.Runtime.Remoting.Proxies;

namespace Tests {
  [TestFixture()]
  public class Test {
    static int buffer;
    
    public static int Identity(int i) {
      buffer = i;
      return i;
    }

    private static object TestMethod(object arg1, object arg2) {
      return 1234;
    }

    [Test]
    public void CompileActionDiscardingRetValue() {
      var p = Expression.Parameter(typeof(int), "i");
      var identity = GetType().GetMethod("Identity", BindingFlags.Static | BindingFlags.Public);
      Assert.IsNotNull(identity);
      
      var lambda = Expression.Lambda<Action<int>>(Expression.Call(identity, p), p);
      
      var method = lambda.Interpret();
      
      buffer = 0;
      
      method(42);
      Assert.AreEqual(42, buffer);
    }

    [Test]
    public void CompileActionDiscardingRetValueSyntax() {
      Expression<Action<int>> lambda = i => Identity(i);
      
      var method = lambda.Interpret();
      
      buffer = 0;
      
      method(42);
      Assert.AreEqual(42, buffer);
    }

    [Test]
    public void ExpressionDelegateTarget() {
      var p = Expression.Parameter(typeof(string), "str");
      var identity = Expression.Lambda<Func<string, string>>(p, p).Interpret();
      
      Assert.AreEqual(typeof(Func<string, string>), identity.GetType());
      Assert.IsNotNull(identity.Target);
    }

    [Test]
    public void SimpleHoistedParameter() {
      var p = Expression.Parameter(typeof(string), "s");
      
      var f = Expression.Lambda<Func<string, Func<string>>>(
        Expression.Lambda<Func<string>>(
        p,
        new ParameterExpression [0]),
        p).Interpret();
      
      var f2 = f("x");
      
      Assert.AreEqual("x", f2());
    }

    [Test]
    public void SimpleHoistedParameterSyntax() {
      var f = ((Expression<Func<string, Func<string>>>)(s => () => s)).Interpret();
      
      var f2 = f("x");
      
      Assert.AreEqual("x", f2());
    }
    
    [Test]
    public void TwoHoistingLevels() {
      var p1 = Expression.Parameter(typeof(string), "x");
      var p2 = Expression.Parameter(typeof(string), "y");
      
      Expression<Func<string, Func<string, Func<string>>>> e =
        Expression.Lambda<Func<string, Func<string, Func<string>>>>(
          Expression.Lambda<Func<string, Func<string>>>(
          Expression.Lambda<Func<string>>(
          Expression.Call(
          typeof(string).GetMethod("Concat", new [] {
          typeof(string),
          typeof(string)
        }),
          new [] { p1, p2 }),
          new ParameterExpression [0]),
          new [] { p2 }),
          new [] { p1 });
      
      var f = e.Interpret();
      var f2 = f("Hello ");
      var f3 = f2("World !");
      
      Assert.AreEqual("Hello World !", f3());
    }

    [Test]
    public void TwoHoistingLevelsSyntax() {
      Expression<Func<string, Func<string, Func<string>>>> e =
        x => y => () => x + y;
      
      var f = e.Interpret();
      var f2 = f("Hello ");
      var f3 = f2("World !");
      
      Assert.AreEqual("Hello World !", f3());
    }

    [Test]
    public void HoistedParameter() {
      var i = Expression.Parameter(typeof(int), "i");
      
      var l = Expression.Lambda<Func<int, string>>(
        Expression.Invoke(
        Expression.Lambda<Func<string>>(
        Expression.Call(i, typeof(int).GetMethod("ToString", Type.EmptyTypes)))), i).Interpret();
      
      Assert.AreEqual("42", l(42));
    }

    [Test]
    public void HoistedParameterSyntax() {
      var l = ((Expression<Func<int, string>>)(i => ((Func<string>)(() => i.ToString()))())).Interpret();
      
      Assert.AreEqual("42", l(42));
    }

    public class S {
      public static int MyAdder(int a, int b) {
        return 1000;
      }
    }

    [Test]
    public void TestMethodAddition() {
      BinaryExpression expr = Expression.Add(Expression.Constant(1), Expression.Constant(2), typeof(S).GetMethod("MyAdder"));
      Expression<Func<int>> l = Expression.Lambda<Func<int>>(expr);
      
      Func<int> compiled = l.Interpret();
      Assert.AreEqual(1000, compiled());
    }

    [Test]
    public void CompileAdd() {
      var left = Expression.Parameter(typeof(int), "l");
      var right = Expression.Parameter(typeof(int), "r");
      var l = Expression.Lambda<Func<int, int, int>>(
        Expression.Add(left, right), left, right);
      
      var be = l.Body as BinaryExpression;
      Assert.IsNotNull(be);
      Assert.AreEqual(typeof(int), be.Type);
      Assert.IsFalse(be.IsLifted);
      Assert.IsFalse(be.IsLiftedToNull);
      
      var add = l.Interpret();
      
      Assert.AreEqual(12, add(6, 6));
      Assert.AreEqual(0, add(-1, 1));
      Assert.AreEqual(-2, add(1, -3));
    }

    [Test]
    public void CompileAddSyntax() {
      Expression<Func<int, int, int>> e = (l, r) => l + r;
      
      var add = e.Interpret();
      
      Assert.AreEqual(12, add(6, 6));
      Assert.AreEqual(0, add(-1, 1));
      Assert.AreEqual(-2, add(1, -3));
    }

    [Test]
    public void AddTestNullable() {
      var a = Expression.Parameter(typeof(int?), "a");
      var b = Expression.Parameter(typeof(int?), "b");
      var l = Expression.Lambda<Func<int?, int?, int?>>(
        Expression.Add(a, b), a, b);
      
      var be = l.Body as BinaryExpression;
      Assert.IsNotNull(be);
      Assert.AreEqual(typeof(int?), be.Type);
      Assert.IsTrue(be.IsLifted);
      Assert.IsTrue(be.IsLiftedToNull);
      
      var c = l.Interpret();
      
      Assert.AreEqual(null, c(1, null), "a1");
      Assert.AreEqual(null, c(null, null), "a2");
      Assert.AreEqual(null, c(null, 2), "a3");
      Assert.AreEqual(3, c(1, 2), "a4");
    }

    [Test]
    public void AddTestNullableSyntax() {
      Expression<Func<int?, int?, int?>> l = (x, y) => x + y;
      
      var be = l.Body as BinaryExpression;
      Assert.IsNotNull(be);
      Assert.AreEqual(typeof(int?), be.Type);
      Assert.IsTrue(be.IsLifted);
      Assert.IsTrue(be.IsLiftedToNull);
      
      var c = l.Interpret();
      
      Assert.AreEqual(null, c(1, null), "a1");
      Assert.AreEqual(null, c(null, null), "a2");
      Assert.AreEqual(null, c(null, 2), "a3");
      Assert.AreEqual(3, c(1, 2), "a4");
    }

    struct Slot {
      public int Value;
      
      public Slot(int value) : this() {
        this.Value = value;
      }
      
      public static Slot operator+(Slot a, Slot b) {
        return new Slot(a.Value + b.Value);
      }
      
      public static Slot operator &(Slot a, Slot b) {
        return new Slot(a.Value & b.Value);
      }

      public static Slot operator |(Slot a, Slot b) {
        return new Slot(a.Value | b.Value);
      }
      
      public static bool operator true(Slot a) {
        return a.Value != 0;
      }
      
      public static bool operator false(Slot a) {
        return a.Value == 0;
      }

      public static bool operator >(Slot a, Slot b) {
        return a.Value > b.Value;
      }
      
      public static bool operator <(Slot a, Slot b) {
        return a.Value < b.Value;
      }

      public static bool operator >=(Slot a, Slot b) {
        return a.Value >= b.Value;
      }
      
      public static bool operator <=(Slot a, Slot b) {
        return a.Value <= b.Value;
      }

      public static Slot operator -(Slot s) {
        return new Slot(-s.Value);
      }

      public static bool operator !(Slot s) {
        return s.Value > 0;
      }
      
      public override string ToString() {
        return Value.ToString();
      }

      public static implicit operator int(Slot s) {
        return s.Value;
      }

      public int Integer { get; set; }

      public short Short { get; set; }

      public override bool Equals(object obj) {
        if (!(obj is Slot)) {
          return false;
        }
        
        var other = (Slot)obj;
        return other.Value == this.Value;
      }
      
      public override int GetHashCode() {
        return Value;
      }
      
      public static bool operator ==(Slot a, Slot b) {
        return a.Value == b.Value;
      }
      
      public static bool operator !=(Slot a, Slot b) {
        return a.Value != b.Value;
      }
    }
    
    [Test]
    public void UserDefinedAdd() {
      var l = Expression.Parameter(typeof(Slot), "l");
      var r = Expression.Parameter(typeof(Slot), "r");
      
      var node = Expression.Add(l, r);
      
      Assert.IsFalse(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(Slot), node.Type);
      
      var add = Expression.Lambda<Func<Slot, Slot, Slot>>(node, l, r).Interpret();
      
      Assert.AreEqual(new Slot(42), add(new Slot(21), new Slot(21)));
      Assert.AreEqual(new Slot(0), add(new Slot(1), new Slot(-1)));
    }

    [Test]
    public void UserDefinedAddSyntax() {
      Expression<Func<Slot, Slot, Slot>> e = (l, r) => l + r;
      var add = e.Interpret();
      
      Assert.AreEqual(new Slot(42), add(new Slot(21), new Slot(21)));
      Assert.AreEqual(new Slot(0), add(new Slot(1), new Slot(-1)));
    }
    
    [Test]
    public void UserDefinedAddLifted() {
      var l = Expression.Parameter(typeof(Slot?), "l");
      var r = Expression.Parameter(typeof(Slot?), "r");
      
      var node = Expression.Add(l, r);
      
      Assert.IsTrue(node.IsLifted);
      Assert.IsTrue(node.IsLiftedToNull);
      Assert.AreEqual(typeof(Slot?), node.Type);
      
      var add = Expression.Lambda<Func<Slot?, Slot?, Slot?>>(node, l, r).Interpret();
      
      Assert.AreEqual(null, add(null, null));
      Assert.AreEqual((Slot?)new Slot(42), add((Slot?)new Slot(21), (Slot?)new Slot(21)));
    }

    [Test]
    public void UserDefinedAddLiftedSyntax() {
      Expression<Func<Slot?, Slot?, Slot?>> e = (l, r) => l + r;
      var add = e.Interpret();
      
      Assert.AreEqual(null, add(null, null));
      Assert.AreEqual((Slot?)new Slot(42), add((Slot?)new Slot(21), (Slot?)new Slot(21)));
    }

    struct SlotToNullable {
      public int Value;
      
      public SlotToNullable(int value) {
        this.Value = value;
      }
      
      public static SlotToNullable? operator +(SlotToNullable a, SlotToNullable b) {
        return new SlotToNullable(a.Value + b.Value);
      }

      public override int GetHashCode() {
        return Value;
      }
      
      public override bool Equals(object obj) {
        if (!(obj is SlotToNullable)) {
          return false;
        }
        
        var other = (SlotToNullable)obj;
        return other.Value == this.Value;
      }
      
      public static bool? operator ==(SlotToNullable a, SlotToNullable b) {
        return (bool?)(a.Value == b.Value);
      }
      
      public static bool? operator !=(SlotToNullable a, SlotToNullable b) {
        return (bool?)(a.Value != b.Value);
      }

      public static SlotToNullable? operator -(SlotToNullable s) {
        return new SlotToNullable(-s.Value);
      }
    }

    [Test]
    public void UserDefinedToNullableAdd() {
      var l = Expression.Parameter(typeof(SlotToNullable), "l");
      var r = Expression.Parameter(typeof(SlotToNullable), "r");
      
      var node = Expression.Add(l, r);
      
      Assert.IsFalse(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(SlotToNullable?), node.Type);
      Assert.IsNotNull(node.Method);
      
      var add = Expression.Lambda<Func<SlotToNullable, SlotToNullable, SlotToNullable?>>(node, l, r).Interpret();
      
      Assert.AreEqual((SlotToNullable?)new SlotToNullable(4), add(new SlotToNullable(2), new SlotToNullable(2)));
      Assert.AreEqual((SlotToNullable?)new SlotToNullable(0), add(new SlotToNullable(2), new SlotToNullable(-2)));
    }

    [Test]
    public void UserDefinedToNullableAddSyntax() {
      Expression<Func<SlotToNullable, SlotToNullable, SlotToNullable?>> e = (l, r) => l + r;
      var add = e.Interpret();
      
      Assert.AreEqual((SlotToNullable?)new SlotToNullable(4), add(new SlotToNullable(2), new SlotToNullable(2)));
      Assert.AreEqual((SlotToNullable?)new SlotToNullable(0), add(new SlotToNullable(2), new SlotToNullable(-2)));
    }

    [Test]
    public void AddStrings() {
      var l = Expression.Parameter(typeof(string), "l");
      var r = Expression.Parameter(typeof(string), "r");
      
      var meth = typeof(string).GetMethod("Concat", new [] {
        typeof(object),
        typeof(object)
      });
      
      var node = Expression.Add(l, r, meth);
      Assert.IsFalse(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(string), node.Type);
      Assert.AreEqual(meth, node.Method);
      
      var concat = Expression.Lambda<Func<string, string, string>>(node, l, r).Interpret();
      
      Assert.AreEqual(string.Empty, concat(null, null));
      Assert.AreEqual("foobar", concat("foo", "bar"));
    }

    [Test]
    public void AddStringsSyntax() {
      Expression<Func<string, string, string>> e = (l, r) => l + r;
      var concat = e.Interpret();
      
      Assert.AreEqual(string.Empty, concat(null, null));
      Assert.AreEqual("foobar", concat("foo", "bar"));
    }
    
    [Test]
    public void AddDecimals() {
      var l = Expression.Parameter(typeof(decimal), "l");
      var r = Expression.Parameter(typeof(decimal), "r");
      
      var meth = typeof(decimal).GetMethod("op_Addition", new [] {
        typeof(decimal),
        typeof(decimal)
      });
      
      var node = Expression.Add(l, r);
      Assert.IsFalse(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(decimal), node.Type);
      Assert.AreEqual(meth, node.Method);
      
      var add = Expression.Lambda<Func<decimal, decimal, decimal>>(node, l, r).Interpret();
      
      Assert.AreEqual(2m, add(1m, 1m));
    }

    [Test]
    public void AddDecimalsSyntax() {
      Expression<Func<decimal, decimal, decimal>> e = (l, r) => l + r;
      var add = e.Interpret();
      
      Assert.AreEqual(2m, add(1m, 1m));
    }
    
    [Test]
    public void AddLiftedDecimals() {
      var l = Expression.Parameter(typeof(decimal?), "l");
      var r = Expression.Parameter(typeof(decimal?), "r");
      
      var meth = typeof(decimal).GetMethod("op_Addition", new [] {
        typeof(decimal),
        typeof(decimal)
      });
      
      var node = Expression.Add(l, r);
      Assert.IsTrue(node.IsLifted);
      Assert.IsTrue(node.IsLiftedToNull);
      Assert.AreEqual(typeof(decimal?), node.Type);
      Assert.AreEqual(meth, node.Method);
      
      var add = Expression.Lambda<Func<decimal?, decimal?, decimal?>>(node, l, r).Interpret();
      
      Assert.AreEqual(2m, add(1m, 1m));
      Assert.AreEqual(null, add(1m, null));
      Assert.AreEqual(null, add(null, null));
    }

    [Test]
    public void AddLiftedDecimalsSyntax() {
      Expression<Func<decimal?, decimal?, decimal?>> e = (l, r) => l + r;
      var add = e.Interpret();
      
      Assert.AreEqual(2m, add(1m, 1m));
      Assert.AreEqual(null, add(1m, null));
      Assert.AreEqual(null, add(null, null));
    }
    
    //
    // This method makes sure that compiling an AddChecked on two values
    // throws an OverflowException, if it doesnt, it fails
    //
    static void MustOverflow<T>(T v1, T v2) {
      Expression<Func<T>> l = Expression.Lambda<Func<T>>(
        Expression.AddChecked(Expression.Constant(v1), Expression.Constant(v2)));
      Func<T> del = l.Interpret();
      T res = default (T);
      try {
        res = del();
      } catch (OverflowException) {
        // OK
        return;
      }
      throw new Exception(String.Format("AddChecked on {2} should have thrown an exception with values {0} {1}, result was: {3}",
                                        v1, v2, v1.GetType(), res));
    }
    
    //
    // This routine should execute the code, but not throw an
    // overflow exception
    //
    static void MustNotOverflow<T>(T v1, T v2) {
      Expression<Func<T>> l = Expression.Lambda<Func<T>>(
        Expression.AddChecked(Expression.Constant(v1), Expression.Constant(v2)));
      Func<T> del = l.Interpret();
      del();
    }
    
    //
    // SubtractChecked is not defined for small types (byte, sbyte)
    //
    static void InvalidOperation<T>(T v1, T v2) {
      try {
        Expression.Lambda<Func<T>>(
          Expression.AddChecked(Expression.Constant(v1), Expression.Constant(v2)));
      } catch (InvalidOperationException) {
        // OK
        return;
      }
      throw new Exception(String.Format("AddChecked should have thrown for the creation of a tree with {0} operands", v1.GetType()));
    }
    
    [Test]
    public void TestOverflows() {
      // These should overflow, check the various types and codepaths
      // in BinaryExpression:
      MustOverflow<int>(Int32.MaxValue, 1);
      MustOverflow<int>(Int32.MinValue, -11);
      MustOverflow<long>(Int64.MaxValue, 1);
      MustOverflow<long>(Int64.MinValue, -1);
      
      // unsigned values use Add_Ovf_Un, check that too:
      MustOverflow<ulong>(UInt64.MaxValue, 1);
      MustOverflow<uint>(UInt32.MaxValue, 1);
    }

    //
    // These should not overflow
    //
    [Test]
    public void TestNoOverflow() {
      // Simple stuff
      MustNotOverflow<int>(10, 20);
      
      // These are invalid:
      InvalidOperation<byte>(Byte.MaxValue, 2);
      InvalidOperation<sbyte>(SByte.MaxValue, 2);
      // Stuff that just fits in 32 bits, does not overflow:
      MustNotOverflow<int>(Int16.MaxValue, 2);
      MustNotOverflow<int>(Int16.MaxValue, 2);
      MustNotOverflow<uint>(UInt16.MaxValue, 2);
      // Doubles, floats, do not overflow
      MustNotOverflow<float>(Single.MaxValue, 1);
      MustNotOverflow<double>(Double.MaxValue, 1);
    }

    [Test]
    public void AndBoolTest() {
      var a = Expression.Parameter(typeof(bool), "a");
      var b = Expression.Parameter(typeof(bool), "b");
      var l = Expression.Lambda<Func<bool, bool, bool>>(
        Expression.And(a, b), a, b);
      
      var be = l.Body as BinaryExpression;
      Assert.IsNotNull(be);
      Assert.AreEqual(typeof(bool), be.Type);
      Assert.IsFalse(be.IsLifted);
      Assert.IsFalse(be.IsLiftedToNull);
      
      var c = l.Interpret();
      
      Assert.AreEqual(true, c(true, true), "t1");
      Assert.AreEqual(false, c(true, false), "t2");
      Assert.AreEqual(false, c(false, true), "t3");
      Assert.AreEqual(false, c(false, false), "t4");
    }

    [Test]
    public void AndBoolTestSyntax() {
      Expression<Func<bool, bool, bool>> l = (a, b) => a & b;
      var c = l.Interpret();
      
      Assert.AreEqual(true, c(true, true), "t1");
      Assert.AreEqual(false, c(true, false), "t2");
      Assert.AreEqual(false, c(false, true), "t3");
      Assert.AreEqual(false, c(false, false), "t4");
    }

    [Test]
    public void AndBoolNullableTest() {
      var a = Expression.Parameter(typeof(bool?), "a");
      var b = Expression.Parameter(typeof(bool?), "b");
      var l = Expression.Lambda<Func<bool?, bool?, bool?>>(
        Expression.And(a, b), a, b);
      
      var be = l.Body as BinaryExpression;
      Assert.IsNotNull(be);
      Assert.AreEqual(typeof(bool?), be.Type);
      Assert.IsTrue(be.IsLifted);
      Assert.IsTrue(be.IsLiftedToNull);
      
      var c = l.Interpret();
      
      Assert.AreEqual(true, c(true, true), "a1");
      Assert.AreEqual(false, c(true, false), "a2");
      Assert.AreEqual(false, c(false, true), "a3");
      Assert.AreEqual(false, c(false, false), "a4");
      
      Assert.AreEqual(null, c(true, null), "a5");
      Assert.AreEqual(false, c(false, null), "a6");
      Assert.AreEqual(false, c(null, false), "a7");
      Assert.AreEqual(null, c(true, null), "a8");
      Assert.AreEqual(null, c(null, null), "a9");
    }

    [Test]
    public void AndBoolNullableTestSyntax() {
      Expression<Func<bool?, bool?, bool?>> l = (a, b) => a & b;
      var c = l.Interpret();
      
      Assert.AreEqual(true, c(true, true), "a1");
      Assert.AreEqual(false, c(true, false), "a2");
      Assert.AreEqual(false, c(false, true), "a3");
      Assert.AreEqual(false, c(false, false), "a4");
      
      Assert.AreEqual(null, c(true, null), "a5");
      Assert.AreEqual(false, c(false, null), "a6");
      Assert.AreEqual(false, c(null, false), "a7");
      Assert.AreEqual(null, c(true, null), "a8");
      Assert.AreEqual(null, c(null, null), "a9");
    }
    
    [Test]
    public void AndBoolItem() {
      var i = Expression.Parameter(typeof(Item<bool>), "i");
      var and = Expression.Lambda<Func<Item<bool>, bool>>(
        Expression.And(
        Expression.Property(i, "Left"),
        Expression.Property(i, "Right")), i).Interpret();
      
      var item = new Item<bool>(false, true);
      Assert.AreEqual(false, and(item));
      Assert.IsTrue(item.LeftCalled);
      Assert.IsTrue(item.RightCalled);
    }

    [Test]
    public void AndBoolItemSyntax() {
      Expression<Func<Item<bool>, bool>> e = i => i.Left & i.Right;
      var and = e.Interpret();
      
      var item = new Item<bool>(false, true);
      Assert.AreEqual(false, and(item));
      Assert.IsTrue(item.LeftCalled);
      Assert.IsTrue(item.RightCalled);
    }
    
    [Test]
    public void AndNullableBoolItem() {
      var i = Expression.Parameter(typeof(Item<bool?>), "i");
      var and = Expression.Lambda<Func<Item<bool?>, bool?>>(
        Expression.And(
        Expression.Property(i, "Left"),
        Expression.Property(i, "Right")), i).Interpret();
      
      var item = new Item<bool?>(false, true);
      Assert.AreEqual((bool?)false, and(item));
      Assert.IsTrue(item.LeftCalled);
      Assert.IsTrue(item.RightCalled);
    }
    
    [Test]
    public void AndIntTest() {
      var a = Expression.Parameter(typeof(int), "a");
      var b = Expression.Parameter(typeof(int), "b");
      var and = Expression.Lambda<Func<int, int, int>>(
        Expression.And(a, b), a, b).Interpret();
      
      Assert.AreEqual(0, and(0, 0), "t1");
      Assert.AreEqual(0, and(0, 1), "t2");
      Assert.AreEqual(0, and(1, 0), "t3");
      Assert.AreEqual(1, and(1, 1), "t4");
    }
    
    [Test]
    public void AndIntNullableTest() {
      var a = Expression.Parameter(typeof(int?), "a");
      var b = Expression.Parameter(typeof(int?), "b");
      var c = Expression.Lambda<Func<int?, int?, int?>>(
        Expression.And(a, b), a, b).Interpret();
      
      Assert.AreEqual((int?)1, c(1, 1), "a1");
      Assert.AreEqual((int?)0, c(1, 0), "a2");
      Assert.AreEqual((int?)0, c(0, 1), "a3");
      Assert.AreEqual((int?)0, c(0, 0), "a4");
      
      Assert.AreEqual((int?)null, c(1, null), "a5");
      Assert.AreEqual((int?)null, c(0, null), "a6");
      Assert.AreEqual((int?)null, c(null, 0), "a7");
      Assert.AreEqual((int?)null, c(1, null), "a8");
      Assert.AreEqual((int?)null, c(null, null), "a9");
    }

    [Test]
    public void AndAlsoTest() {
      var a = Expression.Parameter(typeof(bool), "a");
      var b = Expression.Parameter(typeof(bool), "b");
      var l = Expression.Lambda<Func<bool, bool, bool>>(
        Expression.AndAlso(a, b), a, b);
      
      var be = l.Body as BinaryExpression;
      Assert.IsNotNull(be);
      Assert.AreEqual(typeof(bool), be.Type);
      Assert.IsFalse(be.IsLifted);
      Assert.IsFalse(be.IsLiftedToNull);
      
      var c = l.Interpret();
      
      Assert.AreEqual(true, c(true, true), "a1");
      Assert.AreEqual(false, c(true, false), "a2");
      Assert.AreEqual(false, c(false, true), "a3");
      Assert.AreEqual(false, c(false, false), "a4");
    }

    [Test]
    public void AndAlsoBoolItem() {
      var i = Expression.Parameter(typeof(Item<bool>), "i");
      var and = Expression.Lambda<Func<Item<bool>, bool>>(
        Expression.AndAlso(
        Expression.Property(i, "Left"),
        Expression.Property(i, "Right")), i).Interpret();
      
      var item = new Item<bool>(false, true);
      Assert.AreEqual(false, and(item));
      Assert.IsTrue(item.LeftCalled);
      Assert.IsFalse(item.RightCalled);
    }
    
    [Test]
    public void AndAlsoNullableBoolItem() {
      var i = Expression.Parameter(typeof(Item<bool?>), "i");
      var and = Expression.Lambda<Func<Item<bool?>, bool?>>(
        Expression.AndAlso(
        Expression.Property(i, "Left"),
        Expression.Property(i, "Right")), i).Interpret();
      
      var item = new Item<bool?>(false, true);
      Assert.AreEqual((bool?)false, and(item));
      Assert.IsTrue(item.LeftCalled);
      Assert.IsFalse(item.RightCalled);
    }
    
    [Test]
    public void UserDefinedAndAlso() {
      var l = Expression.Parameter(typeof(Slot), "l");
      var r = Expression.Parameter(typeof(Slot), "r");
      
      var method = typeof(Slot).GetMethod("op_BitwiseAnd");
      
      var node = Expression.AndAlso(l, r, method);
      Assert.IsFalse(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(method, node.Method);
      
      var andalso = Expression.Lambda<Func<Slot, Slot, Slot>>(node, l, r).Interpret();
      
      Assert.AreEqual(new Slot(64), andalso(new Slot(64), new Slot(64)));
      Assert.AreEqual(new Slot(0), andalso(new Slot(32), new Slot(64)));
      Assert.AreEqual(new Slot(0), andalso(new Slot(64), new Slot(32)));
    }
    
    [Test]
    public void UserDefinedAndAlsoShortCircuit() {
      var i = Expression.Parameter(typeof(Item<Slot>), "i");
      var and = Expression.Lambda<Func<Item<Slot>, Slot>>(
        Expression.AndAlso(
        Expression.Property(i, "Left"),
        Expression.Property(i, "Right")), i).Interpret();

      var item = new Item<Slot>(new Slot(0), new Slot(1));
      Assert.AreEqual(new Slot(0), and(item));
      Assert.IsTrue(item.LeftCalled);
      Assert.IsFalse(item.RightCalled);
    }
    
    [Test]
    [Category ("NotDotNet")]
    // https://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=350228
    public void UserDefinedLiftedAndAlsoShortCircuit() {
      var i = Expression.Parameter(typeof(Item<Slot?>), "i");
      var and = Expression.Lambda<Func<Item<Slot?>, Slot?>>(
        Expression.AndAlso(
        Expression.Property(i, "Left"),
        Expression.Property(i, "Right")), i).Interpret();
      
      var item = new Item<Slot?>(null, new Slot(1));
      Assert.AreEqual((Slot?)null, and(item));
      Assert.IsTrue(item.LeftCalled);
      Assert.IsFalse(item.RightCalled);
    }
    
    [Test]
    public void UserDefinedAndAlsoLiftedToNull() {
      var l = Expression.Parameter(typeof(Slot?), "l");
      var r = Expression.Parameter(typeof(Slot?), "r");
      
      var method = typeof(Slot).GetMethod("op_BitwiseAnd");
      
      var node = Expression.AndAlso(l, r, method);
      Assert.IsTrue(node.IsLifted);
      Assert.IsTrue(node.IsLiftedToNull);
      Assert.AreEqual(method, node.Method);
      
      var andalso = Expression.Lambda<Func<Slot?, Slot?, Slot?>>(node, l, r).Interpret();
      
      Assert.AreEqual(new Slot(64), andalso(new Slot(64), new Slot(64)));
      Assert.AreEqual(new Slot(0), andalso(new Slot(32), new Slot(64)));
      Assert.AreEqual(new Slot(0), andalso(new Slot(64), new Slot(32)));
      Assert.AreEqual(null, andalso(null, new Slot(32)));
      Assert.AreEqual(null, andalso(new Slot(64), null));
      Assert.AreEqual(null, andalso(null, null));
    }
    
    struct Incomplete {
      public int Value;
      
      public Incomplete(int val) {
        Value = val;
      }
      
      public static Incomplete operator &(Incomplete a, Incomplete b) {
        return new Incomplete(a.Value & b.Value);
      }
    }

    class A {
      public static bool operator true(A x) {
        return true;
      }
      
      public static bool operator false(A x) {
        return false;
      }
    }
    
    class B : A {
      public static B operator &(B x, B y) {
        return new B();
      }
      
      public static bool op_True<T>(B x) {
        return true;
      }
      
      public static bool op_False(B x) {
        return false;
      }
    }
    
    [Test]
    // from https://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=350487
    public void Connect350487() {
      var p = Expression.Parameter(typeof(B), "b");
      var l = Expression.Lambda<Func<B, A>>(
        Expression.AndAlso(p, p), p).Interpret();
      
      Assert.IsNotNull(l(null));
    }

    static Func<T [], int, T> CreateArrayAccess<T>() {
      var a = Expression.Parameter(typeof(T[]), "a");
      var i = Expression.Parameter(typeof(int), "i");
      
      return Expression.Lambda<Func<T[], int, T>>(
        Expression.ArrayIndex(a, i), a, i).Interpret();
    }
    
    [Test]
    public void CompileIntArrayAccess() {
      var array = new int [] { 1, 2, 3, 4 };
      var at = CreateArrayAccess<int>();
      
      Assert.AreEqual(1, at(array, 0));
      Assert.AreEqual(4, at(array, 3));
    }
    
    [Test]
    public void CompileShortArrayAccess() {
      var array = new short [] { 1, 2, 3, 4 };
      var at = CreateArrayAccess<short>();
      
      Assert.AreEqual(array[0], at(array, 0));
      Assert.AreEqual(array[3], at(array, 3));
    }
    
    enum Months {
      Jan,
      Feb,
      Mar,
      Apr }
    ;
    
    [Test]
    public void CompileEnumArrayAccess() {
      var array = new Months [] {
        Months.Jan,
        Months.Feb,
        Months.Mar,
        Months.Apr
      };
      var at = CreateArrayAccess<Months>();
      
      Assert.AreEqual(array[0], at(array, 0));
      Assert.AreEqual(array[3], at(array, 3));
    }
    
    class Foo {
    }
    
    [Test]
    public void CompileClassArrayAccess() {
      var array = new Foo [] { new Foo(), new Foo(), new Foo(), new Foo() };
      var at = CreateArrayAccess<Foo>();
      
      Assert.AreEqual(array[0], at(array, 0));
      Assert.AreEqual(array[3], at(array, 3));
    }
    
    struct Bar {
      public int bar;

      public Bar(int b) {
        bar = b;
      }
    }
    
    [Test]
    public void CompileStructArrayAccess() {
      var array = new Bar [] {
        new Bar(0),
        new Bar(1),
        new Bar(2),
        new Bar(3)
      };
      var at = CreateArrayAccess<Bar>();
      
      Assert.AreEqual(array[0], at(array, 0));
      Assert.AreEqual(array[3], at(array, 3));
    }

    [Test]
    public void CompileArrayLength() {
      var p = Expression.Parameter(typeof(object[]), "ary");
      var len = Expression.Lambda<Func<object[], int>>(
        Expression.ArrayLength(p), p).Interpret();
      
      Assert.AreEqual(0, len(new string [0]));
      Assert.AreEqual(2, len(new [] { "jb", "evain" }));
    }

    [Test]
    public void BindValueTypes() {
      var i = Expression.Parameter(typeof(int), "i");
      var s = Expression.Parameter(typeof(short), "s");
      
      var gslot = Expression.Lambda<Func<int, short, Slot>>(
        Expression.MemberInit(
        Expression.New(typeof(Slot)),
        Expression.Bind(typeof(Slot).GetProperty("Integer"), i),
        Expression.Bind(typeof(Slot).GetProperty("Short"), s)), i, s).Interpret();
      
      Assert.AreEqual(new Slot { Integer = 42, Short = -1 }, gslot(42, -1));
    }

    public static object Identity2(object o) {
      return o;
    }

    [Test]
    public void CompileSimpleStaticCall() {
      var p = Expression.Parameter(typeof(object), "o");
      var lambda = Expression.Lambda<Func<object, object>>(Expression.Call(GetType().GetMethod("Identity2"), p), p);
      
      var i = lambda.Interpret();
      
      Assert.AreEqual(2, i(2));
      Assert.AreEqual("Foo", i("Foo"));
    }
    
    [Test]
    public void CompileSimpleInstanceCall() {
      var p = Expression.Parameter(typeof(string), "p");
      var lambda = Expression.Lambda<Func<string, string>>(
        Expression.Call(
        p, typeof(string).GetMethod("ToString", Type.EmptyTypes)),
        p);
      
      var ts = lambda.Interpret();
      
      Assert.AreEqual("foo", ts("foo"));
      Assert.AreEqual("bar", ts("bar"));
    }

    public struct EineStrukt {
      
      public string Foo;
      
      public EineStrukt(string foo) {
        Foo = foo;
      }
      
      public string GimmeFoo() {
        return Foo;
      }
    }
    
    [Test]
    public void CallMethodOnStruct() {
      var param = Expression.Parameter(typeof(EineStrukt), "s");
      var foo = Expression.Lambda<Func<EineStrukt, string>>(
        Expression.Call(param, typeof(EineStrukt).GetMethod("GimmeFoo")), param).Interpret();
      
      var s = new EineStrukt("foo");
      Assert.AreEqual("foo", foo(s));
    }
    
    public static int OneStaticMethod() {
      return 42;
    }

    public static int DoSomethingWith(ref int a) {
      return a + 4;
    }
    
    public static string DoAnotherThing(ref int a, string s) {
      return s + a;
    }
    
    [Test]
    public void CallStaticMethodWithRefParameter() {
      var p = Expression.Parameter(typeof(int), "i");
      
      var c = Expression.Lambda<Func<int, int>>(
        Expression.Call(GetType().GetMethod("DoSomethingWith"), p), p).Interpret();
      
      Assert.AreEqual(42, c(38));
    }
    
    [Test]
    public void CallStaticMethodWithRefParameterAndOtherParameter() {
      var i = Expression.Parameter(typeof(int), "i");
      var s = Expression.Parameter(typeof(string), "s");
      
      var lamda = Expression.Lambda<Func<int, string, string>>(
        Expression.Call(GetType().GetMethod("DoAnotherThing"), i, s), i, s).Interpret();
      
      Assert.AreEqual("foo42", lamda(42, "foo"));
    }
    
    public static int Bang(Expression i) {
      return (int)(i as ConstantExpression).Value;
    }

    [Test]
    public void CallMethodWithExpressionParameter() {
      var call = Expression.Call(GetType().GetMethod("Bang"), Expression.Constant(Expression.Constant(42)));
      Assert.AreEqual(ExpressionType.Constant, call.Arguments[0].NodeType);
      
      var l = Expression.Lambda<Func<int>>(call).Interpret();
      
      Assert.AreEqual(42, l());
    }

    static bool fout_called = false;
    
    public static int FooOut(out int x) {
      fout_called = true;
      return x = 0;
    }
    
    [Test]
    public void Connect282729() {
      // test from https://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=282729
      
      var p = Expression.Parameter(typeof(int), "p");
      var lambda = Expression.Lambda<Func<int, int>>(
        Expression.Call(
        GetType().GetMethod("FooOut"),
        Expression.ArrayIndex(
        Expression.NewArrayBounds(
        typeof(int),
        1.ToConstant()),
        0.ToConstant())),
        p).Interpret();
      
      Assert.AreEqual(0, lambda(0));
      Assert.IsTrue(fout_called);
    }
    
    public static int FooOut2(out int x) {
      x = 2;
      return 3;
    }
    
    [Test]
    [Category ("NotWorking")]
    public void Connect290278() {
      // test from https://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=290278
      
//      var p = Expression.Parameter(typeof(int[,]), "p");
//      var lambda = Expression.Lambda<Func<int[,], int>>(
//        Expression.Call(
//        GetType().GetMethod("FooOut2"),
//        Expression.ArrayIndex(p, 0.ToConstant(), 0.ToConstant())),
//        p).Interpret();
      Expression<Func<int[,], int>> lambda = p => FooOut2(out p[0L, 0]);

      int [,] data = { { 1 } };

      Assert.AreEqual(3, lambda.Interpret()(data));
      Assert.AreEqual(2, data[0, 0]);
    }
    
    public static void FooRef(ref string s) {
    }
    
    [Test]
    public void Connect297597() {
      // test from https://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=297597
      
      var strings = new string [1];
      
      var lambda = Expression.Lambda<Action>(
        Expression.Call(
        GetType().GetMethod("FooRef"),
        Expression.ArrayIndex(
        Expression.Constant(strings), 0.ToConstant()))).Interpret();
      
      lambda();
    }
    
    public static int Truc() {
      return 42;
    }
    
    [Test]
    public void Connect282702() {
      var lambda = Expression.Lambda<Func<Func<int>>>(
        Expression.Convert(
        Expression.Call(
        typeof(Delegate).GetMethod("CreateDelegate", new [] {
        typeof(Type),
        typeof(object),
        typeof(MethodInfo)
      }),
        Expression.Constant(typeof(Func<int>), typeof(Type)),
        Expression.Constant(null, typeof(object)),
        Expression.Constant(GetType().GetMethod("Truc"))),
        typeof(Func<int>))).Interpret();
      
      Assert.AreEqual(42, lambda().Invoke());
    }

    [Test]
    public void CallNullableGetValueOrDefault() { // #568989
      var value = Expression.Parameter(typeof(int?), "value");
      var default_parameter = Expression.Parameter(typeof(int), "default");

      var getter = Expression.Lambda<Func<int?, int, int>>(
        Expression.Call(
        value,
        "GetValueOrDefault",
        Type.EmptyTypes,
        default_parameter),
        value,
        default_parameter).Interpret();

      Assert.AreEqual(2, getter(null, 2));
      Assert.AreEqual(4, getter(4, 2));
    }

    [Test]
    public void CompileCallNullableGetValueOrDefaultSyntax() { // #568989
      Expression<Func<int?, int, int>> getterExpr = (o1, o2) => o1.GetValueOrDefault(o2);
      var getter = getterExpr.Interpret();

      Assert.AreEqual(2, getter(null, 2));
      Assert.AreEqual(4, getter(4, 2));
    }
    
    [Test]
    public void CallToStringOnEnum() { // #625367
      var lambda = Expression.Lambda<Func<string>>(
        Expression.Call(
        Expression.Constant(TypeCode.Boolean, typeof(TypeCode)),
        typeof(object).GetMethod("ToString"))).Interpret();
      
      Assert.AreEqual("Boolean", lambda());
    }

    [Test]
    public void CoalesceNullableInt() {
      var a = Expression.Parameter(typeof(int?), "a");
      var b = Expression.Parameter(typeof(int?), "b");
      var coalesce = Expression.Lambda<Func<int?, int?, int?>>(
        Expression.Coalesce(a, b), a, b).Interpret();
      
      Assert.AreEqual((int?)1, coalesce(1, 2));
      Assert.AreEqual((int?)null, coalesce(null, null));
      Assert.AreEqual((int?)2, coalesce(null, 2));
      Assert.AreEqual((int?)2, coalesce(2, null));
    }
    
    [Test]
    public void CoalesceString() {
      var a = Expression.Parameter(typeof(string), "a");
      var b = Expression.Parameter(typeof(string), "b");
      var coalesce = Expression.Lambda<Func<string, string, string>>(
        Expression.Coalesce(a, b), a, b).Interpret();
      
      Assert.AreEqual("foo", coalesce("foo", "bar"));
      Assert.AreEqual(null, coalesce(null, null));
      Assert.AreEqual("bar", coalesce(null, "bar"));
      Assert.AreEqual("foo", coalesce("foo", null));
    }
    
    [Test]
    public void CoalesceNullableToNonNullable() {
      var a = Expression.Parameter(typeof(int?), "a");
      
      var node = Expression.Coalesce(a, Expression.Constant(99, typeof(int)));
      
      Assert.AreEqual(typeof(int), node.Type);
      Assert.IsFalse(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      
      var coalesce = Expression.Lambda<Func<int?, int>>(node, a).Interpret();
      
      Assert.AreEqual(5, coalesce(5));
      Assert.AreEqual(99, coalesce(null));
    }

    [Test]
    public void CoalesceNullableSlotIntoInteger() {
      var s = Expression.Parameter(typeof(Slot?), "s");
      
      var method = typeof(Slot).GetMethod("op_Implicit");
      
      var coalesce = Expression.Lambda<Func<Slot?, int>>(
        Expression.Coalesce(
        s,
        Expression.Constant(-3),
        Expression.Lambda(
        Expression.Convert(s, typeof(int), method),
        s)), s).Interpret();
      
      Assert.AreEqual(-3, coalesce(null));
      Assert.AreEqual(42, coalesce(new Slot(42)));
    }

    [Test]
    public void CompileConditional() {
      var parameters = new [] { Expression.Parameter(typeof(int), "number") };
      
      var l = Expression.Lambda<Func<int, string>>(
        Expression.Condition(
        Expression.GreaterThanOrEqual(
        parameters[0],
        Expression.Constant(0)),
        Expression.Constant("+"),
        Expression.Constant("-")),
        parameters);
      
      var gt = l.Interpret();
      
      Assert.AreEqual("+", gt(1));
      Assert.AreEqual("+", gt(0));
      Assert.AreEqual("-", gt(-1));
    }

    static T Check<T>(T val) {
      Expression<Func<T>> l = Expression.Lambda<Func<T>>(Expression.Constant(val), new ParameterExpression [0]);
      Func<T> fi = l.Interpret();
      return fi();
    }

    [Test]
    public void ConstantCodeGen() {
      Assert.AreEqual(Check<int>(0), 0, "int");
      Assert.AreEqual(Check<int>(128), 128, "int2");
      Assert.AreEqual(Check<int>(-128), -128, "int3");
      Assert.AreEqual(Check<int>(Int32.MinValue), Int32.MinValue, "int4");
      Assert.AreEqual(Check<int>(Int32.MaxValue), Int32.MaxValue, "int5");
      Assert.AreEqual(Check<uint>(128), 128, "uint");
      Assert.AreEqual(Check<uint>(0), 0, "uint2");
      Assert.AreEqual(Check<uint>(UInt32.MinValue), UInt32.MinValue, "uint3");
      Assert.AreEqual(Check<uint>(UInt32.MaxValue), UInt32.MaxValue, "uint4");
      Assert.AreEqual(Check<byte>(10), 10, "byte");
      Assert.AreEqual(Check<byte>(Byte.MinValue), Byte.MinValue, "byte2");
      Assert.AreEqual(Check<byte>(Byte.MaxValue), Byte.MaxValue, "byte3");
      Assert.AreEqual(Check<short>(128), 128, "short");
      Assert.AreEqual(Check<short>(-128), -128, "short");
      Assert.AreEqual(Check<short>(Int16.MinValue), Int16.MinValue, "short2");
      Assert.AreEqual(Check<short>(Int16.MaxValue), Int16.MaxValue, "short3");
      Assert.AreEqual(Check<ushort>(128), 128, "ushort");
      Assert.AreEqual(Check<ushort>(UInt16.MinValue), UInt16.MinValue, "short2");
      Assert.AreEqual(Check<ushort>(UInt16.MaxValue), UInt16.MaxValue, "short3");
      Assert.AreEqual(Check<bool>(true), true, "bool1");
      Assert.AreEqual(Check<bool>(false), false, "bool2");
      Assert.AreEqual(Check<long>(Int64.MaxValue), Int64.MaxValue, "long");
      Assert.AreEqual(Check<long>(Int64.MinValue), Int64.MinValue, "long2");
      Assert.AreEqual(Check<ulong>(UInt64.MaxValue), UInt64.MaxValue, "ulong");
      Assert.AreEqual(Check<ulong>(UInt64.MinValue), UInt64.MinValue, "ulong2");
      Assert.AreEqual(Check<ushort>(200), 200, "ushort");
      Assert.AreEqual(Check<float>(2.0f), 2.0f, "float");
      Assert.AreEqual(Check<double>(2.312), 2.312, "double");
      Assert.AreEqual(Check<string>("dingus"), "dingus", "string");
      Assert.AreEqual(Check<decimal>(1.3m), 1.3m, "");
      
      // this forces the other code path for decimal.
      Assert.AreEqual(Check<decimal>(3147483647m), 3147483647m, "decimal");
    }

    [Test]
    public void EmitDateTimeConstant() {
      var date = new DateTime(1983, 2, 6);
      
      var lambda = Expression.Lambda<Func<DateTime>>(Expression.Constant(date)).Interpret();
      
      Assert.AreEqual(date, lambda());
    }
    
    [Test]
    public void EmitDBNullConstant() {
      var lambda = Expression.Lambda<Func<DBNull>>(Expression.Constant(DBNull.Value)).Interpret();
      
      Assert.AreEqual(DBNull.Value, lambda());
    }
    
    [Test]
    public void EmitNullString() {
      var n = Expression.Lambda<Func<string>>(
        Expression.Constant(null, typeof(string))).Interpret();
      
      Assert.IsNull(n());
    }
    
    [Test]
    public void EmitNullNullableType() {
      var n = Expression.Lambda<Func<int?>>(
        Expression.Constant(null, typeof(int?))).Interpret();
      
      Assert.IsNull(n());
    }
    
    [Test]
    public void EmitNullableInt() {
      var i = Expression.Lambda<Func<int?>>(
        Expression.Constant((int?)42, typeof(int?))).Interpret();
      
      Assert.AreEqual((int?)42, i());
    }

    enum Chose {
      Moche
    }
    
    [Test]
    public void EmitNullableEnum() {
      var e = Expression.Lambda<Func<Chose?>>(
        Expression.Constant((Chose?)Chose.Moche, typeof(Chose?))).Interpret();
      
      Assert.AreEqual((Chose?)Chose.Moche, e());
    }

    class Klang {
      int i;
      
      public Klang(int i) {
        this.i = i;
      }
      
      public static explicit operator int(Klang k) {
        return k.i;
      }
    }

    struct Kling {
      int i;
      
      public Kling(int i) {
        this.i = i;
      }
      
      public static implicit operator int(Kling k) {
        return k.i;
      }
    }

    interface IFoo {
    }

    class Foo2 : IFoo {
    }

    class Bar2 : Foo2 {
    }

    class Baz {
    }
    
    interface ITzap {
    }

    [Test]
    public void CompileConvertClassWithExplicitOp() {
      var p = Expression.Parameter(typeof(Klang), "klang");
      var c = Expression.Lambda<Func<Klang, int>>(
        Expression.Convert(p, typeof(int)), p).Interpret();
      
      Assert.AreEqual(42, c(new Klang(42)));
    }

    [Test]
    public void CompileConvertStructWithImplicitOp() {
      var p = Expression.Parameter(typeof(Kling), "kling");
      var c = Expression.Lambda<Func<Kling, int>>(
        Expression.Convert(p, typeof(int)), p).Interpret();
      
      Assert.AreEqual(42, c(new Kling(42)));
    }

    [Test]
    public void CompiledBoxing() {
      var b = Expression.Lambda<Func<object>>(
        Expression.Convert(42.ToConstant(), typeof(object))).Interpret();
      
      Assert.AreEqual((object)42, b());
    }
    
    [Test]
    public void CompiledUnBoxing() {
      var p = Expression.Parameter(typeof(object), "o");
      
      var u = Expression.Lambda<Func<object, int>>(
        Expression.Convert(p, typeof(int)), p).Interpret();
      
      Assert.AreEqual(42, u((object)42));
    }
    
    [Test]
    public void CompiledCast() {
      var p = Expression.Parameter(typeof(IFoo), "foo");
      
      var c = Expression.Lambda<Func<IFoo, Bar2>>(
        Expression.Convert(p, typeof(Bar2)), p).Interpret();
      
      IFoo foo = new Bar2();
      
      Bar2 b = c(foo);
      
      Assert.AreEqual(b, foo);
    }
    
    [Test]
    public void CompileNotNullableToNullable() {
      var p = Expression.Parameter(typeof(int), "i");
      var c = Expression.Lambda<Func<int, int?>>(
        Expression.Convert(p, typeof(int?)), p).Interpret();
      
      Assert.AreEqual((int?)0, c(0));
      Assert.AreEqual((int?)42, c(42));
    }
    
    [Test]
    public void CompileNullableToNotNullable() {
      var p = Expression.Parameter(typeof(int?), "i");
      var c = Expression.Lambda<Func<int?, int>>(
        Expression.Convert(p, typeof(int)), p).Interpret();
      
      Assert.AreEqual(0, c((int?)0));
      Assert.AreEqual(42, c((int?)42));
      
      Action a = () => c(null);
      
      a.AssertThrows(typeof(InvalidCastException));
    }
    
    [Test]
    public void CompiledConvertToSameType() {
      var k = new Klang(42);
      
      var p = Expression.Parameter(typeof(Klang), "klang");
      var c = Expression.Lambda<Func<Klang, Klang>>(
        Expression.Convert(
        p, typeof(Klang)),
        p).Interpret();
      
      Assert.AreEqual(k, c(k));
    }
    
    [Test]
    public void CompiledConvertNullableToNullable() {
      var p = Expression.Parameter(typeof(int?), "i");
      var c = Expression.Lambda<Func<int?, short?>>(
        Expression.Convert(p, typeof(short?)), p).Interpret();
      
      Assert.AreEqual((short?)null, c(null));
      Assert.AreEqual((short?)12, c(12));
    }
    
    [Test]
    public void CompiledNullableBoxing() {
      var p = Expression.Parameter(typeof(int?), "i");
      var c = Expression.Lambda<Func<int?, object>>(
        Expression.Convert(p, typeof(object)), p).Interpret();
      
      Assert.AreEqual(null, c(null));
      Assert.AreEqual((object)(int?)42, c(42));
    }
    
    [Test]
    public void CompiledNullableUnboxing() {
      var p = Expression.Parameter(typeof(object), "o");
      var c = Expression.Lambda<Func<object, int?>>(
        Expression.Convert(p, typeof(int?)), p).Interpret();
      
      Assert.AreEqual((int?)null, c(null));
      Assert.AreEqual((int?)42, c((int?)42));
    }
    
    [Test]
    public void ChainedNullableConvert() {
      var p = Expression.Parameter(typeof(sbyte?), "a");
      
      var test = Expression.Lambda<Func<sbyte?, long?>>(
        Expression.Convert(
        Expression.Convert(
        p,
        typeof(int?)),
        typeof(long?)), p).Interpret();
      
      Assert.AreEqual((long?)3, test((sbyte?)3));
      Assert.AreEqual(null, test(null));
    }

    struct ImplicitToShort {
      short value;
      
      public ImplicitToShort(short v) {
        value = v;
      }
      
      public static implicit operator short(ImplicitToShort i) {
        return i.value;
      }
    }
    
    [Test]
    public void ConvertImplicitToShortToNullableInt() {
      var a = Expression.Parameter(typeof(ImplicitToShort?), "a");
      
      var method = typeof(ImplicitToShort).GetMethod("op_Implicit");
      
      var node = Expression.Convert(a, typeof(short), method);
      Assert.IsTrue(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(short), node.Type);
      Assert.AreEqual(method, node.Method);
      
      var conv = Expression.Lambda<Func<ImplicitToShort?, int?>>(
        Expression.Convert(
        node,
        typeof(int?)), a).Interpret();
      
      Assert.AreEqual((int?)42, conv(new ImplicitToShort(42)));
      
      Action convnull = () => Assert.AreEqual(null, conv(null));
      
      convnull.AssertThrows(typeof(InvalidOperationException));
    }
    
    [Test]
    public void NullableImplicitToShort() {
      var i = Expression.Parameter(typeof(ImplicitToShort?), "i");
      
      var method = typeof(ImplicitToShort).GetMethod("op_Implicit");
      
      var node = Expression.Convert(i, typeof(short?), method);
      
      Assert.IsTrue(node.IsLifted);
      Assert.IsTrue(node.IsLiftedToNull);
      Assert.AreEqual(typeof(short?), node.Type);
      Assert.AreEqual(method, node.Method);
      
      var convert = Expression.Lambda<Func<ImplicitToShort?, short?>>(node, i).Interpret();
      
      Assert.AreEqual((short?)42, convert(new ImplicitToShort(42)));
    }
    
    [Test]
    public void ConvertLongToDecimal() {
      var p = Expression.Parameter(typeof(long), "l");
      
      var node = Expression.Convert(p, typeof(decimal));
      Assert.IsFalse(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(decimal), node.Type);
      Assert.IsNotNull(node.Method);
      
      var convert = Expression.Lambda<Func<long, decimal>>(node, p).Interpret();
      
      Assert.AreEqual(42, convert(42));
    }
    
    [Test]
    public void ConvertNullableULongToNullableDecimal() {
      var p = Expression.Parameter(typeof(ulong?), "l");
      
      var node = Expression.Convert(p, typeof(decimal?));
      Assert.IsTrue(node.IsLifted);
      Assert.IsTrue(node.IsLiftedToNull);
      Assert.AreEqual(typeof(decimal?), node.Type);
      Assert.IsNotNull(node.Method);
      
      var convert = Expression.Lambda<Func<ulong?, decimal?>>(node, p).Interpret();
      
      Assert.AreEqual(42, convert(42));
      Assert.AreEqual(null, convert(null));
    }

    struct ImplicitToInt {
      int Value;
      
      public ImplicitToInt(int v) {
        Value = v;
      }
      
      public static implicit operator int(ImplicitToInt i) {
        return i.Value;
      }
    }
    
    [Test]
    public void ConvertNullableImplictToIntToNullableLong() {
      var i = Expression.Parameter(typeof(ImplicitToInt?), "i");
      
      var method = typeof(ImplicitToInt).GetMethod("op_Implicit");
      
      var node = Expression.Convert(i, typeof(int), method);
      node = Expression.Convert(node, typeof(long?));
      var conv = Expression.Lambda<Func<ImplicitToInt?, long?>>(node, i).Interpret();
      
      Assert.AreEqual((long?)42, conv(new ImplicitToInt(42)));
      Action convnull = () => Assert.AreEqual(null, conv(null));
      convnull.AssertThrows(typeof(InvalidOperationException));
    }

    [Test]
    public void NullableInt32Equal() {
      var l = Expression.Parameter(typeof(int?), "l");
      var r = Expression.Parameter(typeof(int?), "r");
      
      var eq = Expression.Lambda<Func<int?, int?, bool>>(
        Expression.Equal(l, r), l, r).Interpret();
      
      Assert.IsTrue(eq(null, null));
      Assert.IsFalse(eq(null, 1));
      Assert.IsFalse(eq(1, null));
      Assert.IsFalse(eq(1, 2));
      Assert.IsTrue(eq(1, 1));
      Assert.IsFalse(eq(null, 0));
      Assert.IsFalse(eq(0, null));
    }
    
    [Test]
    public void NullableInt32EqualLiftedToNull() {
      var l = Expression.Parameter(typeof(int?), "l");
      var r = Expression.Parameter(typeof(int?), "r");
      
      var eq = Expression.Lambda<Func<int?, int?, bool?>>(
        Expression.Equal(l, r, true, null), l, r).Interpret();
      
      Assert.AreEqual((bool?)null, eq(null, null));
      Assert.AreEqual((bool?)null, eq(null, 1));
      Assert.AreEqual((bool?)null, eq(1, null));
      Assert.AreEqual((bool?)false, eq(1, 2));
      Assert.AreEqual((bool?)true, eq(1, 1));
      Assert.AreEqual((bool?)null, eq(null, 0));
      Assert.AreEqual((bool?)null, eq(0, null));
    }

    [Test]
    public void UserDefinedEqual() {
      var l = Expression.Parameter(typeof(Slot), "l");
      var r = Expression.Parameter(typeof(Slot), "r");
      
      var node = Expression.Equal(l, r);
      
      Assert.IsFalse(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(bool), node.Type);
      Assert.IsNotNull(node.Method);
      
      var eq = Expression.Lambda<Func<Slot, Slot, bool>>(node, l, r).Interpret();
      
      Assert.AreEqual(true, eq(new Slot(21), new Slot(21)));
      Assert.AreEqual(false, eq(new Slot(1), new Slot(-1)));
    }
    
    [Test]
    public void UserDefinedEqualLifted() {
      var l = Expression.Parameter(typeof(Slot?), "l");
      var r = Expression.Parameter(typeof(Slot?), "r");
      
      var node = Expression.Equal(l, r);
      
      Assert.IsTrue(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(bool), node.Type);
      Assert.IsNotNull(node.Method);
      
      var eq = Expression.Lambda<Func<Slot?, Slot?, bool>>(node, l, r).Interpret();
      
      Assert.AreEqual(true, eq(null, null));
      Assert.AreEqual(false, eq((Slot?)new Slot(2), null));
      Assert.AreEqual(false, eq(null, (Slot?)new Slot(2)));
      Assert.AreEqual(true, eq((Slot?)new Slot(21), (Slot?)new Slot(21)));
    }
    
    [Test]
    public void UserDefinedEqualLiftedToNull() {
      var l = Expression.Parameter(typeof(Slot?), "l");
      var r = Expression.Parameter(typeof(Slot?), "r");
      
      var node = Expression.Equal(l, r, true, null);
      
      Assert.IsTrue(node.IsLifted);
      Assert.IsTrue(node.IsLiftedToNull);
      Assert.AreEqual(typeof(bool?), node.Type);
      Assert.IsNotNull(node.Method);
      
      var eq = Expression.Lambda<Func<Slot?, Slot?, bool?>>(node, l, r).Interpret();
      
      Assert.AreEqual((bool?)null, eq(null, null));
      Assert.AreEqual((bool?)null, eq((Slot?)new Slot(2), null));
      Assert.AreEqual((bool?)null, eq(null, (Slot?)new Slot(2)));
      Assert.AreEqual((bool?)true, eq((Slot?)new Slot(21), (Slot?)new Slot(21)));
      Assert.AreEqual((bool?)false, eq((Slot?)new Slot(21), (Slot?)new Slot(-21)));
    }

    [Test]
    public void UserDefinedToNullableEqual() {
      var l = Expression.Parameter(typeof(SlotToNullable), "l");
      var r = Expression.Parameter(typeof(SlotToNullable), "r");
      
      var node = Expression.Equal(l, r, false, null);
      
      Assert.IsFalse(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(bool?), node.Type);
      Assert.IsNotNull(node.Method);
      
      var eq = Expression.Lambda<Func<SlotToNullable, SlotToNullable, bool?>>(node, l, r).Interpret();
      
      Assert.AreEqual((bool?)true, eq(new SlotToNullable(2), new SlotToNullable(2)));
      Assert.AreEqual((bool?)false, eq(new SlotToNullable(2), new SlotToNullable(-2)));
    }

    [Test]
    public void NullableBoolEqualToBool() {
      var l = Expression.Parameter(typeof(bool?), "l");
      var r = Expression.Parameter(typeof(bool?), "r");
      
      var node = Expression.Equal(l, r);
      Assert.IsTrue(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(bool), node.Type);
      Assert.IsNull(node.Method);
      
      var eq = Expression.Lambda<Func<bool?, bool?, bool>>(node, l, r).Interpret();
      
      Assert.AreEqual(false, eq(true, null));
      Assert.AreEqual(true, eq(null, null));
      Assert.AreEqual(true, eq(false, false));
    }
    
    public enum FooEnum {
      Bar,
      Baz,
    }
    
    [Test]
    public void EnumEqual() {
      var l = Expression.Parameter(typeof(FooEnum), "l");
      var r = Expression.Parameter(typeof(FooEnum), "r");
      
      var node = Expression.Equal(l, r);
      Assert.IsFalse(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(bool), node.Type);
      Assert.IsNull(node.Method);
      
      var eq = Expression.Lambda<Func<FooEnum, FooEnum, bool>>(node, l, r).Interpret();
      
      Assert.AreEqual(true, eq(FooEnum.Bar, FooEnum.Bar));
      Assert.AreEqual(false, eq(FooEnum.Bar, FooEnum.Baz));
    }
    
    [Test]
    public void LiftedEnumEqual() {
      var l = Expression.Parameter(typeof(FooEnum?), "l");
      var r = Expression.Parameter(typeof(FooEnum?), "r");
      
      var node = Expression.Equal(l, r);
      Assert.IsTrue(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(bool), node.Type);
      Assert.IsNull(node.Method);
      
      var eq = Expression.Lambda<Func<FooEnum?, FooEnum?, bool>>(node, l, r).Interpret();
      
      Assert.AreEqual(true, eq(FooEnum.Bar, FooEnum.Bar));
      Assert.AreEqual(false, eq(FooEnum.Bar, FooEnum.Baz));
      Assert.AreEqual(false, eq(FooEnum.Bar, null));
      Assert.AreEqual(true, eq(null, null));
    }

    public static string foo = "foo";
    
    [Test]
    public void CompileStaticField() {
      var foo = Expression.Lambda<Func<string>>(
        Expression.Field(null, GetType().GetField(
        "foo", BindingFlags.Static | BindingFlags.Public))).Interpret();
      
      Assert.AreEqual("foo", foo());
    }
    
    public class Bar3 {
      public string baz;
      
      public Bar3() {
        baz = "baz";
      }
    }
    
    [Test]
    public void CompileInstanceField() {
      var p = Expression.Parameter(typeof(Bar3), "bar");
      var baz = Expression.Lambda<Func<Bar3, string>>(
        Expression.Field(p, typeof(Bar3).GetField(
        "baz", BindingFlags.Public | BindingFlags.Instance)), p).Interpret();
      
      Assert.AreEqual("baz", baz(new Bar3()));
    }
    
    public struct Gazonk {
      public string Tzap;
      
      public Gazonk(string tzap) {
        Tzap = tzap;
      }
    }
    
    [Test]
    public void CompileStructInstanceField() {
      var p = Expression.Parameter(typeof(Gazonk), "gazonk");
      var gazonker = Expression.Lambda<Func<Gazonk, string>>(
        Expression.Field(p, typeof(Gazonk).GetField("Tzap")), p).Interpret();
      
      Assert.AreEqual("bang", gazonker(new Gazonk("bang")));
    }

    [Test]
    public void TestCompiled() {
      ParameterExpression a = Expression.Parameter(typeof(int), "a");
      ParameterExpression b = Expression.Parameter(typeof(int), "b");
      
      BinaryExpression p = Expression.GreaterThan(a, b);
      
      Expression<Func<int,int,bool>> pexpr = Expression.Lambda<Func<int,int,bool>>(
        p, new ParameterExpression [] { a, b });
      
      Func<int,int,bool> compiled = pexpr.Interpret();
      Assert.AreEqual(true, compiled(10, 1), "tc1");
      Assert.AreEqual(true, compiled(1, 0), "tc2");
      Assert.AreEqual(true, compiled(Int32.MinValue + 1, Int32.MinValue), "tc3");
      Assert.AreEqual(false, compiled(-1, 0), "tc4");
      Assert.AreEqual(false, compiled(0, Int32.MaxValue), "tc5");
    }
    
    [Test]
    public void NullableInt32GreaterThan() {
      var l = Expression.Parameter(typeof(int?), "l");
      var r = Expression.Parameter(typeof(int?), "r");
      
      var gt = Expression.Lambda<Func<int?, int?, bool>>(
        Expression.GreaterThan(l, r), l, r).Interpret();
      
      Assert.IsFalse(gt(null, null));
      Assert.IsFalse(gt(null, 1));
      Assert.IsFalse(gt(null, -1));
      Assert.IsFalse(gt(1, null));
      Assert.IsFalse(gt(-1, null));
      Assert.IsFalse(gt(1, 2));
      Assert.IsTrue(gt(2, 1));
      Assert.IsFalse(gt(1, 1));
    }
    
    [Test]
    public void NullableInt32GreaterThanLiftedToNull() {
      var l = Expression.Parameter(typeof(int?), "l");
      var r = Expression.Parameter(typeof(int?), "r");
      
      var gt = Expression.Lambda<Func<int?, int?, bool?>>(
        Expression.GreaterThan(l, r, true, null), l, r).Interpret();
      
      Assert.AreEqual((bool?)null, gt(null, null));
      Assert.AreEqual((bool?)null, gt(null, 1));
      Assert.AreEqual((bool?)null, gt(null, -1));
      Assert.AreEqual((bool?)null, gt(1, null));
      Assert.AreEqual((bool?)null, gt(-1, null));
      Assert.AreEqual((bool?)false, gt(1, 2));
      Assert.AreEqual((bool?)true, gt(2, 1));
      Assert.AreEqual((bool?)false, gt(1, 1));
    }

    [Test]
    public void UserDefinedGreaterThanLifted() {
      var l = Expression.Parameter(typeof(Slot?), "l");
      var r = Expression.Parameter(typeof(Slot?), "r");
      
      var node = Expression.GreaterThan(l, r);
      Assert.IsTrue(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(bool), node.Type);
      Assert.IsNotNull(node.Method);
      
      var gte = Expression.Lambda<Func<Slot?, Slot?, bool>>(node, l, r).Interpret();
      
      Assert.AreEqual(true, gte(new Slot(1), new Slot(0)));
      Assert.AreEqual(false, gte(new Slot(-1), new Slot(1)));
      Assert.AreEqual(false, gte(new Slot(1), new Slot(1)));
      Assert.AreEqual(false, gte(null, new Slot(1)));
      Assert.AreEqual(false, gte(new Slot(1), null));
      Assert.AreEqual(false, gte(null, null));
    }
    
    [Test]
    public void UserDefinedGreaterThanLiftedToNull() {
      var l = Expression.Parameter(typeof(Slot?), "l");
      var r = Expression.Parameter(typeof(Slot?), "r");
      
      var node = Expression.GreaterThan(l, r, true, null);
      Assert.IsTrue(node.IsLifted);
      Assert.IsTrue(node.IsLiftedToNull);
      Assert.AreEqual(typeof(bool?), node.Type);
      Assert.IsNotNull(node.Method);
      
      var gte = Expression.Lambda<Func<Slot?, Slot?, bool?>>(node, l, r).Interpret();
      
      Assert.AreEqual(true, gte(new Slot(1), new Slot(0)));
      Assert.AreEqual(false, gte(new Slot(-1), new Slot(1)));
      Assert.AreEqual(false, gte(new Slot(1), new Slot(1)));
      Assert.AreEqual(null, gte(null, new Slot(1)));
      Assert.AreEqual(null, gte(new Slot(1), null));
      Assert.AreEqual(null, gte(null, null));
    }

    [Test]
    public void NullableInt32GreaterThanOrEqual() {
      var l = Expression.Parameter(typeof(int?), "l");
      var r = Expression.Parameter(typeof(int?), "r");
      
      var gte = Expression.Lambda<Func<int?, int?, bool>>(
        Expression.GreaterThanOrEqual(l, r), l, r).Interpret();
      
      Assert.IsFalse(gte(null, null));
      Assert.IsFalse(gte(null, 1));
      Assert.IsFalse(gte(null, -1));
      Assert.IsFalse(gte(1, null));
      Assert.IsFalse(gte(-1, null));
      Assert.IsFalse(gte(1, 2));
      Assert.IsTrue(gte(2, 1));
      Assert.IsTrue(gte(1, 1));
    }
    
    [Test]
    public void NullableInt32GreaterThanOrEqualLiftedToNull() {
      var l = Expression.Parameter(typeof(int?), "l");
      var r = Expression.Parameter(typeof(int?), "r");
      
      var gte = Expression.Lambda<Func<int?, int?, bool?>>(
        Expression.GreaterThanOrEqual(l, r, true, null), l, r).Interpret();
      
      Assert.AreEqual((bool?)null, gte(null, null));
      Assert.AreEqual((bool?)null, gte(null, 1));
      Assert.AreEqual((bool?)null, gte(null, -1));
      Assert.AreEqual((bool?)null, gte(1, null));
      Assert.AreEqual((bool?)null, gte(-1, null));
      Assert.AreEqual((bool?)false, gte(1, 2));
      Assert.AreEqual((bool?)true, gte(2, 1));
      Assert.AreEqual((bool?)true, gte(1, 1));
    }

    [Test]
    public void UserDefinedGreaterThanOrEqualLifted() {
      var l = Expression.Parameter(typeof(Slot?), "l");
      var r = Expression.Parameter(typeof(Slot?), "r");
      
      var node = Expression.GreaterThanOrEqual(l, r);
      Assert.IsTrue(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(bool), node.Type);
      Assert.IsNotNull(node.Method);
      
      var gte = Expression.Lambda<Func<Slot?, Slot?, bool>>(node, l, r).Interpret();
      
      Assert.AreEqual(true, gte(new Slot(1), new Slot(0)));
      Assert.AreEqual(false, gte(new Slot(-1), new Slot(1)));
      Assert.AreEqual(true, gte(new Slot(1), new Slot(1)));
      Assert.AreEqual(false, gte(null, new Slot(1)));
      Assert.AreEqual(false, gte(new Slot(1), null));
      Assert.AreEqual(false, gte(null, null));
    }
    
    [Test]
    public void UserDefinedGreaterThanOrEqualLiftedToNull() {
      var l = Expression.Parameter(typeof(Slot?), "l");
      var r = Expression.Parameter(typeof(Slot?), "r");
      
      var node = Expression.GreaterThanOrEqual(l, r, true, null);
      Assert.IsTrue(node.IsLifted);
      Assert.IsTrue(node.IsLiftedToNull);
      Assert.AreEqual(typeof(bool?), node.Type);
      Assert.IsNotNull(node.Method);
      
      var gte = Expression.Lambda<Func<Slot?, Slot?, bool?>>(node, l, r).Interpret();
      
      Assert.AreEqual(true, gte(new Slot(1), new Slot(0)));
      Assert.AreEqual(false, gte(new Slot(-1), new Slot(1)));
      Assert.AreEqual(true, gte(new Slot(1), new Slot(1)));
      Assert.AreEqual(null, gte(null, new Slot(1)));
      Assert.AreEqual(null, gte(new Slot(1), null));
      Assert.AreEqual(null, gte(null, null));
    }

    delegate string StringAction(string s);
    
    static string Identity(string s) {
      return s;
    }
    
    [Test]
    public void TestCompileInvokePrivateDelegate() {
      var action = Expression.Parameter(typeof(StringAction), "action");
      var str = Expression.Parameter(typeof(string), "str");
      var invoker = Expression.Lambda<Func<StringAction, string, string>>(
        Expression.Invoke(action, str), action, str).Interpret();
      
      Assert.AreEqual("foo", invoker(Identity, "foo"));
    }
    
    [Test]
    public void InvokeWithExpressionLambdaAsArguments() {
      var p = Expression.Parameter(typeof(string), "s");
      
      Func<string, Expression<Func<string, string>>, string> caller = (s, f) => f.Interpret().Invoke(s);
      
      var invoke = Expression.Lambda<Func<string>>(
        Expression.Invoke(
        Expression.Constant(caller),
        Expression.Constant("KABOOM!"),
        Expression.Lambda<Func<string, string>>(
        Expression.Call(p, typeof(string).GetMethod("ToLowerInvariant")), p)));
      
      Assert.AreEqual(ExpressionType.Quote,
                       (invoke.Body as InvocationExpression).Arguments[1].NodeType);
      
      Assert.AreEqual("kaboom!", invoke.Interpret().DynamicInvoke());
    }

    [Test]
    [ExpectedException(typeof(InvalidOperationException))]
    public void ParameterOutOfScope() {
      ParameterExpression a = Expression.Parameter(typeof(int), "a");
      ParameterExpression second_a = Expression.Parameter(typeof(int), "a");
      
      // Here we have the same name for the parameter expression, but
      // we pass a different object to the Lambda expression, so they are
      // different, this should throw
      Expression<Func<int,int>> l = Expression.Lambda<Func<int,int>>(a, new ParameterExpression [] { second_a });
      l.Interpret()(1);
    }

    [Test]
    public void ParameterRefTest() {
      ParameterExpression a = Expression.Parameter(typeof(int), "a");
      ParameterExpression b = Expression.Parameter(typeof(int), "b");
      
      Expression<Func<int,int,int>> l = Expression.Lambda<Func<int,int,int>>(
        Expression.Add(a, b), new ParameterExpression [] { a, b });
      
      Assert.AreEqual(typeof(Func<int, int, int>), l.Type);
      Assert.AreEqual("(a, b) => (a + b)", l.ToString());
      
      Func<int,int,int> xx = l.Interpret();
      int res = xx(10, 20);
      Assert.AreEqual(res, 30);
    }
    
    [Test]
    public void Interpret() {
      Expression<Func<int>> l = Expression.Lambda<Func<int>>(Expression.Constant(1), new ParameterExpression [0]);
      Assert.AreEqual(typeof(Func<int>), l.Type);
      Assert.AreEqual("() => 1", l.ToString());
      
      Func<int> fi = l.Interpret();
      fi();
    }

    [Test]
    public void CompileLeftShift() {
      ParameterExpression l = Expression.Parameter(typeof(int), "l"), r = Expression.Parameter(typeof(int), "r");
      
      var ls = Expression.Lambda<Func<int, int, int>>(
        Expression.LeftShift(l, r), l, r).Interpret();
      
      Assert.AreEqual(12, ls(6, 1));
      Assert.AreEqual(96, ls(12, 3));
    }
    
    [Test]
    public void LeftShiftNullableLongAndInt() {
      var l = Expression.Parameter(typeof(long?), "l");
      var r = Expression.Parameter(typeof(int), "r");
      
      var node = Expression.LeftShift(l, r);
      Assert.IsTrue(node.IsLifted);
      Assert.IsTrue(node.IsLiftedToNull);
      Assert.AreEqual(typeof(long?), node.Type);
      
      var ls = Expression.Lambda<Func<long?, int, long?>>(node, l, r).Interpret();
      
      Assert.AreEqual(null, ls(null, 2));
      Assert.AreEqual(2048, ls(1024, 1));
    }

    [Test]
    public void NullableInt32LessThan() {
      var l = Expression.Parameter(typeof(int?), "l");
      var r = Expression.Parameter(typeof(int?), "r");
      
      var lt = Expression.Lambda<Func<int?, int?, bool>>(
        Expression.LessThan(l, r), l, r).Interpret();
      
      Assert.IsFalse(lt(null, null));
      Assert.IsFalse(lt(null, 1));
      Assert.IsFalse(lt(null, -1));
      Assert.IsFalse(lt(1, null));
      Assert.IsFalse(lt(-1, null));
      Assert.IsTrue(lt(1, 2));
      Assert.IsFalse(lt(2, 1));
      Assert.IsFalse(lt(1, 1));
    }
    
    [Test]
    public void NullableInt32LessThanLiftedToNull() {
      var l = Expression.Parameter(typeof(int?), "l");
      var r = Expression.Parameter(typeof(int?), "r");
      
      var lt = Expression.Lambda<Func<int?, int?, bool?>>(
        Expression.LessThan(l, r, true, null), l, r).Interpret();
      
      Assert.AreEqual((bool?)null, lt(null, null));
      Assert.AreEqual((bool?)null, lt(null, 1));
      Assert.AreEqual((bool?)null, lt(null, -1));
      Assert.AreEqual((bool?)null, lt(1, null));
      Assert.AreEqual((bool?)null, lt(-1, null));
      Assert.AreEqual((bool?)true, lt(1, 2));
      Assert.AreEqual((bool?)false, lt(2, 1));
      Assert.AreEqual((bool?)false, lt(1, 1));
    }

    [Test]
    public void UserDefinedLessThanLifted() {
      var l = Expression.Parameter(typeof(Slot?), "l");
      var r = Expression.Parameter(typeof(Slot?), "r");
      
      var node = Expression.LessThan(l, r);
      Assert.IsTrue(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(bool), node.Type);
      Assert.IsNotNull(node.Method);
      
      var lte = Expression.Lambda<Func<Slot?, Slot?, bool>>(node, l, r).Interpret();
      
      Assert.AreEqual(false, lte(new Slot(1), new Slot(0)));
      Assert.AreEqual(true, lte(new Slot(-1), new Slot(1)));
      Assert.AreEqual(false, lte(new Slot(1), new Slot(1)));
      Assert.AreEqual(false, lte(null, new Slot(1)));
      Assert.AreEqual(false, lte(new Slot(1), null));
      Assert.AreEqual(false, lte(null, null));
    }
    
    [Test]
    public void UserDefinedLessThanLiftedToNull() {
      var l = Expression.Parameter(typeof(Slot?), "l");
      var r = Expression.Parameter(typeof(Slot?), "r");
      
      var node = Expression.LessThan(l, r, true, null);
      Assert.IsTrue(node.IsLifted);
      Assert.IsTrue(node.IsLiftedToNull);
      Assert.AreEqual(typeof(bool?), node.Type);
      Assert.IsNotNull(node.Method);
      
      var lte = Expression.Lambda<Func<Slot?, Slot?, bool?>>(node, l, r).Interpret();
      
      Assert.AreEqual(false, lte(new Slot(1), new Slot(0)));
      Assert.AreEqual(true, lte(new Slot(-1), new Slot(1)));
      Assert.AreEqual(false, lte(new Slot(1), new Slot(1)));
      Assert.AreEqual(null, lte(null, new Slot(1)));
      Assert.AreEqual(null, lte(new Slot(1), null));
      Assert.AreEqual(null, lte(null, null));
    }

    [Test]
    public void NullableInt32LessThanOrEqual() {
      var l = Expression.Parameter(typeof(int?), "l");
      var r = Expression.Parameter(typeof(int?), "r");
      
      var lte = Expression.Lambda<Func<int?, int?, bool>>(
        Expression.LessThanOrEqual(l, r), l, r).Interpret();
      
      Assert.IsFalse(lte(null, null));
      Assert.IsFalse(lte(null, 1));
      Assert.IsFalse(lte(null, -1));
      Assert.IsFalse(lte(1, null));
      Assert.IsFalse(lte(-1, null));
      Assert.IsTrue(lte(1, 2));
      Assert.IsFalse(lte(2, 1));
      Assert.IsTrue(lte(1, 1));
    }
    
    [Test]
    public void NullableInt32LessThanOrEqualLiftedToNull() {
      var l = Expression.Parameter(typeof(int?), "l");
      var r = Expression.Parameter(typeof(int?), "r");
      
      var lte = Expression.Lambda<Func<int?, int?, bool?>>(
        Expression.LessThanOrEqual(l, r, true, null), l, r).Interpret();
      
      Assert.AreEqual((bool?)null, lte(null, null));
      Assert.AreEqual((bool?)null, lte(null, 1));
      Assert.AreEqual((bool?)null, lte(null, -1));
      Assert.AreEqual((bool?)null, lte(1, null));
      Assert.AreEqual((bool?)null, lte(-1, null));
      Assert.AreEqual((bool?)true, lte(1, 2));
      Assert.AreEqual((bool?)false, lte(2, 1));
      Assert.AreEqual((bool?)true, lte(1, 1));
    }

    [Test]
    public void UserDefinedLessThanOrEqualLifted() {
      var l = Expression.Parameter(typeof(Slot?), "l");
      var r = Expression.Parameter(typeof(Slot?), "r");
      
      var node = Expression.LessThanOrEqual(l, r);
      Assert.IsTrue(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(bool), node.Type);
      Assert.IsNotNull(node.Method);
      
      var lte = Expression.Lambda<Func<Slot?, Slot?, bool>>(node, l, r).Interpret();
      
      Assert.AreEqual(false, lte(new Slot(1), new Slot(0)));
      Assert.AreEqual(true, lte(new Slot(-1), new Slot(1)));
      Assert.AreEqual(true, lte(new Slot(1), new Slot(1)));
      Assert.AreEqual(false, lte(null, new Slot(1)));
      Assert.AreEqual(false, lte(new Slot(1), null));
      Assert.AreEqual(false, lte(null, null));
    }
    
    [Test]
    public void UserDefinedLessThanOrEqualLiftedToNull() {
      var l = Expression.Parameter(typeof(Slot?), "l");
      var r = Expression.Parameter(typeof(Slot?), "r");
      
      var node = Expression.LessThanOrEqual(l, r, true, null);
      Assert.IsTrue(node.IsLifted);
      Assert.IsTrue(node.IsLiftedToNull);
      Assert.AreEqual(typeof(bool?), node.Type);
      Assert.IsNotNull(node.Method);
      
      var lte = Expression.Lambda<Func<Slot?, Slot?, bool?>>(node, l, r).Interpret();
      
      Assert.AreEqual(false, lte(new Slot(1), new Slot(0)));
      Assert.AreEqual(true, lte(new Slot(-1), new Slot(1)));
      Assert.AreEqual(true, lte(new Slot(1), new Slot(1)));
      Assert.AreEqual(null, lte(null, new Slot(1)));
      Assert.AreEqual(null, lte(new Slot(1), null));
      Assert.AreEqual(null, lte(null, null));
    }

    public class Foo3 {
      
      public string[] foo;
      public string str;
      public int baz;
      private List<string> list = new List<string>();
      
      public List<string> List {
        get { return list; }
      }
      
      public string [] Bar {
        get { return foo; }
        set { foo = value; }
      }
      
      public int BarBar {
        get { return 0; }
      }
      
      public string[] test() {
        return null;
      }
    }

    [Test]
    public void CompiledListBinding() {
      var add = typeof(List<string>).GetMethod("Add");
      
      var lb = Expression.Lambda<Func<Foo3>>(
        Expression.MemberInit(
        Expression.New(typeof(Foo3)),
        Expression.ListBind(
        typeof(Foo3).GetProperty("List"),
        Expression.ElementInit(add, Expression.Constant("foo")),
        Expression.ElementInit(add, Expression.Constant("bar")),
        Expression.ElementInit(add, Expression.Constant("baz"))))).Interpret();
      
      var foo = lb();
      
      Assert.IsNotNull(foo);
      Assert.AreEqual(3, foo.List.Count);
      Assert.AreEqual("foo", foo.List[0]);
      Assert.AreEqual("bar", foo.List[1]);
      Assert.AreEqual("baz", foo.List[2]);
    }

    [Test]
    public void CompileListOfStringsInit() {
      var add = typeof(List<string>).GetMethod("Add");
      
      var c = Expression.Lambda<Func<List<string>>>(
        Expression.ListInit(
        Expression.New(typeof(List<string>)),
        Expression.ElementInit(add, "foo".ToConstant()),
        Expression.ElementInit(add, "bar".ToConstant()))).Interpret();
      
      var list = c();
      
      Assert.IsNotNull(list);
      Assert.AreEqual(2, list.Count);
      Assert.AreEqual("foo", list[0]);
      Assert.AreEqual("bar", list[1]);
    }
    
    [Test]
    [Category ("NotDotNet")]
    public void CompileArrayListOfStringsInit() {
      var add = typeof(ArrayList).GetMethod("Add");
      
      var c = Expression.Lambda<Func<ArrayList>>(
        Expression.ListInit(
        Expression.New(typeof(ArrayList)),
        Expression.ElementInit(add, "foo".ToConstant()),
        Expression.ElementInit(add, "bar".ToConstant()))).Interpret();
      
      var list = c();
      
      Assert.IsNotNull(list);
      Assert.AreEqual(2, list.Count);
      Assert.AreEqual("foo", list[0]);
      Assert.AreEqual("bar", list[1]);
    }

    public T CodeGen<T>(Func<Expression, Expression, Expression> bin, T v1, T v2) {
      var lambda = Expression.Lambda<Func<T>>(bin(v1.ToConstant(), v2.ToConstant())).Interpret();
      return lambda();
    }
    
    [Test]
    public void TestOperations() {
      Assert.AreEqual(30, CodeGen<int>((a, b) => Expression.Add(a, b), 10, 20));
      Assert.AreEqual(-12, CodeGen<int>((a, b) => Expression.Subtract(a, b), 11, 23));
      Assert.AreEqual(253, CodeGen<int>((a, b) => Expression.Multiply(a, b), 11, 23));
      Assert.AreEqual(33, CodeGen<int>((a, b) => Expression.Divide(a, b), 100, 3));
      Assert.AreEqual(100.0 / 3, CodeGen<double>((a, b) => Expression.Divide(a, b), 100, 3));
    }

    void CTest<T>(ExpressionType node, bool r, T a, T b) {
      ParameterExpression pa = Expression.Parameter(typeof(T), "a");
      ParameterExpression pb = Expression.Parameter(typeof(T), "b");
      
      BinaryExpression p = Expression.MakeBinary(node, Expression.Constant(a), Expression.Constant(b));
      Expression<Func<T,T,bool>> pexpr = Expression.Lambda<Func<T,T,bool>>(
        p, new ParameterExpression [] { pa, pb });
      
      Func<T,T,bool> compiled = pexpr.Interpret();
      Assert.AreEqual(r, compiled(a, b), String.Format("{0} ({1},{2}) == {3}", node, a, b, r));
    }
    
    [Test]
    public void ComparisonTests() {
      ExpressionType t = ExpressionType.Equal;
      
      CTest<byte>(t, true, 10, 10);
      CTest<sbyte>(t, false, 1, 5);
      CTest<sbyte>(t, true, 1, 1);
      CTest<int>(t, true, 1, 1);
      CTest<double>(t, true, 1.0, 1.0);
      CTest<string>(t, true, "", "");
      CTest<string>(t, true, "Hey", "Hey");
      CTest<string>(t, false, "Hey", "There");
      
      t = ExpressionType.NotEqual;
      
      CTest<byte>(t, false, 10, 10);
      CTest<sbyte>(t, true, 1, 5);
      CTest<sbyte>(t, false, 1, 1);
      CTest<int>(t, false, 1, 1);
      CTest<double>(t, false, 1.0, 1.0);
      CTest<double>(t, false, 1.0, 1.0);
      CTest<string>(t, false, "", "");
      CTest<string>(t, false, "Hey", "Hey");
      CTest<string>(t, true, "Hey", "There");
      
      t = ExpressionType.GreaterThan;
      CTest<byte>(t, true, 5, 1);
      CTest<byte>(t, false, 10, 10);
      CTest<sbyte>(t, false, 1, 5);
      CTest<sbyte>(t, false, 1, 1);
      CTest<int>(t, false, 1, 1);
      CTest<uint>(t, true, 1, 0);
      CTest<ulong>(t, true, Int64.MaxValue, 0);
      CTest<double>(t, false, 1.0, 1.0);
      CTest<double>(t, false, 1.0, 1.0);
      
      
      t = ExpressionType.LessThan;
      CTest<byte>(t, false, 5, 1);
      CTest<byte>(t, false, 10, 10);
      CTest<sbyte>(t, true, 1, 5);
      CTest<sbyte>(t, false, 1, 1);
      CTest<int>(t, false, 1, 1);
      CTest<uint>(t, false, 1, 0);
      CTest<ulong>(t, false, Int64.MaxValue, 0);
      CTest<double>(t, false, 1.0, 1.0);
      CTest<double>(t, false, 1.0, 1.0);
      
      t = ExpressionType.GreaterThanOrEqual;
      CTest<byte>(t, true, 5, 1);
      CTest<byte>(t, true, 10, 10);
      CTest<sbyte>(t, false, 1, 5);
      CTest<sbyte>(t, true, 1, 1);
      CTest<int>(t, true, 1, 1);
      CTest<uint>(t, true, 1, 0);
      CTest<ulong>(t, true, Int64.MaxValue, 0);
      CTest<double>(t, true, 1.0, 1.0);
      CTest<double>(t, true, 1.0, 1.0);
      
      
      t = ExpressionType.LessThanOrEqual;
      CTest<byte>(t, false, 5, 1);
      CTest<byte>(t, true, 10, 10);
      CTest<sbyte>(t, true, 1, 5);
      CTest<sbyte>(t, true, 1, 1);
      CTest<int>(t, true, 1, 1);
      CTest<uint>(t, false, 1, 0);
      CTest<ulong>(t, false, Int64.MaxValue, 0);
      CTest<double>(t, true, 1.0, 1.0);
      CTest<double>(t, true, 1.0, 1.0);
    }

    public class Foo4 {
      public string Bar;
      public string Baz;
      public Gazonk2 Gaz;
      
      public Gazonk2 Gazoo { get; set; }
      
      public string Gruik { get; set; }
      
      public Foo4() {
        Gazoo = new Gazonk2();
        Gaz = new Gazonk2();
      }
    }
    
    public class Gazonk2 {
      public string Tzap;
      public int Klang;
      
      public string Couic { get; set; }
      
      public string Bang() {
        return "";
      }
    }

    [Test]
    public void CompiledMemberBinding() {
      var getfoo = Expression.Lambda<Func<Foo4>>(
        Expression.MemberInit(
        Expression.New(typeof(Foo4)),
        Expression.MemberBind(
        typeof(Foo4).GetProperty("Gazoo"),
        Expression.Bind(typeof(Gazonk2).GetField("Tzap"),
                       "tzap".ToConstant()),
        Expression.Bind(typeof(Gazonk2).GetField("Klang"),
                       42.ToConstant())))).Interpret();
      
      var foo = getfoo();
      
      Assert.IsNotNull(foo);
      Assert.AreEqual("tzap", foo.Gazoo.Tzap);
      Assert.AreEqual(42, foo.Gazoo.Klang);
    }

    public class Thing {
      public string Foo;

      public string Bar { get; set; }
    }
    
    [Test]
    public void CompiledInit() {
      var i = Expression.Lambda<Func<Thing>>(
        Expression.MemberInit(
        Expression.New(typeof(Thing)),
        Expression.Bind(typeof(Thing).GetField("Foo"), "foo".ToConstant()),
        Expression.Bind(typeof(Thing).GetProperty("Bar"), "bar".ToConstant()))).Interpret();
      
      var thing = i();
      Assert.IsNotNull(thing);
      Assert.AreEqual("foo", thing.Foo);
      Assert.AreEqual("bar", thing.Bar);
    }

    [Test]
    public void CompiledModulo() {
      var l = Expression.Parameter(typeof(double), "l");
      var p = Expression.Parameter(typeof(double), "r");
      
      var modulo = Expression.Lambda<Func<double, double, double>>(
        Expression.Modulo(l, p), l, p).Interpret();
      
      Assert.AreEqual(0, modulo(4.0, 2.0));
      Assert.AreEqual(2.0, modulo(5.0, 3.0));
    }

    [Test]
    public void CompileNegateInt32() {
      var p = Expression.Parameter(typeof(int), "i");
      var negate = Expression.Lambda<Func<int, int>>(Expression.Negate(p), p).Interpret();
      
      Assert.AreEqual(-2, negate(2));
      Assert.AreEqual(0, negate(0));
      Assert.AreEqual(3, negate(-3));
    }
    
    [Test]
    public void CompiledNegateNullableInt32() {
      var p = Expression.Parameter(typeof(int?), "i");
      var negate = Expression.Lambda<Func<int?, int?>>(Expression.Negate(p), p).Interpret();
      
      Assert.AreEqual(null, negate(null));
      Assert.AreEqual((int?)-2, negate(2));
      Assert.AreEqual((int?)0, negate(0));
      Assert.AreEqual((int?)3, negate(-3));
    }

    [Test]
    public void UserDefinedNegate() {
      var s = Expression.Parameter(typeof(Slot), "s");
      var node = Expression.Negate(s);
      Assert.IsFalse(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(Slot), node.Type);
      
      var negate = Expression.Lambda<Func<Slot, Slot>>(node, s).Interpret();
      
      Assert.AreEqual(new Slot(-2), negate(new Slot(2)));
      Assert.AreEqual(new Slot(42), negate(new Slot(-42)));
    }
    
    [Test]
    public void UserDefinedNotNullableNegateNullable() {
      var s = Expression.Parameter(typeof(Slot?), "s");
      var node = Expression.Negate(s);
      Assert.IsTrue(node.IsLifted);
      Assert.IsTrue(node.IsLiftedToNull);
      Assert.AreEqual(typeof(Slot?), node.Type);
      
      var negate = Expression.Lambda<Func<Slot?, Slot?>>(node, s).Interpret();
      
      Assert.AreEqual(null, negate(null));
      Assert.AreEqual(new Slot(42), negate(new Slot(-42)));
      Assert.AreEqual(new Slot(-2), negate(new Slot(2)));
    }

    public void UserDefinedToNullableNegateNullable() {
      var s = Expression.Parameter(typeof(SlotToNullable), "s");
      var node = Expression.Negate(s);
      Assert.IsFalse(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(SlotToNullable?), node.Type);
      
      var negate = Expression.Lambda<Func<SlotToNullable, SlotToNullable?>>(node, s).Interpret();
      
      Assert.AreEqual((SlotToNullable?)new SlotToNullable(42), negate(new SlotToNullable(-42)));
      Assert.AreEqual((SlotToNullable?)new SlotToNullable(-2), negate(new SlotToNullable(2)));
    }

    struct SlotFromNullable {
      public int Value;
      
      public SlotFromNullable(int value) {
        this.Value = value;
      }
      
      public static SlotFromNullable operator -(SlotFromNullable? s) {
        if (s.HasValue) {
          return new SlotFromNullable(-s.Value.Value);
        } else {
          return new SlotFromNullable(-1);
        }
      }
    }
    
    [Test]
    public void UserDefinedNegateFromNullable() {
      var s = Expression.Parameter(typeof(SlotFromNullable?), "s");
      var node = Expression.Negate(s);
      Assert.IsFalse(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(SlotFromNullable), node.Type);
      
      var negate = Expression.Lambda<Func<SlotFromNullable?, SlotFromNullable>>(node, s).Interpret();
      
      Assert.AreEqual(new SlotFromNullable(-2), negate(new SlotFromNullable(2)));
      Assert.AreEqual(new SlotFromNullable(42), negate(new SlotFromNullable(-42)));
      Assert.AreEqual(new SlotFromNullable(-1), negate(null));
    }
    
    struct SlotFromNullableToNullable {
      public int Value;
      
      public SlotFromNullableToNullable(int value) {
        this.Value = value;
      }
      
      public static SlotFromNullableToNullable? operator -(SlotFromNullableToNullable? s) {
        if (s.HasValue) {
          return new SlotFromNullableToNullable(-s.Value.Value);
        } else {
          return s;
        }
      }
    }
    
    [Test]
    public void UserDefinedNegateFromNullableNotNullable() {
      var s = Expression.Parameter(typeof(SlotFromNullableToNullable?), "s");
      var node = Expression.Negate(s);
      Assert.IsFalse(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(SlotFromNullableToNullable?), node.Type);
      
      var negate = Expression.Lambda<Func<SlotFromNullableToNullable?, SlotFromNullableToNullable?>>(
        node, s).Interpret();
      
      Assert.AreEqual(new SlotFromNullableToNullable(-2), negate(new SlotFromNullableToNullable(2)));
      Assert.AreEqual(new SlotFromNullableToNullable(42), negate(new SlotFromNullableToNullable(-42)));
      Assert.AreEqual(null, negate(null));
    }
    
    [Test]
    public void NegateDecimal() {
      var d = Expression.Parameter(typeof(decimal), "l");
      
      var meth = typeof(decimal).GetMethod("op_UnaryNegation", new [] { typeof(decimal) });
      
      var node = Expression.Negate(d);
      Assert.IsFalse(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(decimal), node.Type);
      Assert.AreEqual(meth, node.Method);
      
      var neg = Expression.Lambda<Func<decimal, decimal>>(node, d).Interpret();
      
      Assert.AreEqual(-2m, neg(2m));
    }
    
    [Test]
    public void NegateLiftedDecimal() {
      var d = Expression.Parameter(typeof(decimal?), "l");
      
      var meth = typeof(decimal).GetMethod("op_UnaryNegation", new [] { typeof(decimal) });
      
      var node = Expression.Negate(d);
      Assert.IsTrue(node.IsLifted);
      Assert.IsTrue(node.IsLiftedToNull);
      Assert.AreEqual(typeof(decimal?), node.Type);
      Assert.AreEqual(meth, node.Method);
      
      var neg = Expression.Lambda<Func<decimal?, decimal?>>(node, d).Interpret();
      
      Assert.AreEqual(-2m, neg(2m));
      Assert.AreEqual(null, neg(null));
    }

    public class Bar4 {
      
      public string Value { get; set; }
      
      public Bar4() {
      }
    }

    [Test]
    public void CompileNewClass() {
      var p = Expression.Parameter(typeof(string), "p");
      var n = Expression.New(typeof(Gazonk).GetConstructor(new [] { typeof(string) }), p);
      var fgaz = Expression.Lambda<Func<string, Gazonk>>(n, p).Interpret();
      
      var g1 = new Gazonk("foo");
      var g2 = new Gazonk("bar");
      
      Assert.IsNotNull(g1);
      Assert.AreEqual(g1, fgaz("foo"));
      Assert.IsNotNull(g2);
      Assert.AreEqual(g2, fgaz("bar"));
      
      n = Expression.New(typeof(Bar4));
      var lbar = Expression.Lambda<Func<Bar4>>(n).Interpret();
      
      var bar = lbar();
      
      Assert.IsNotNull(bar);
      Assert.IsNull(bar.Value);
    }

    public struct EineStrukt2 {
      public int left;
      public int right;
      
      public EineStrukt2(int left, int right) {
        this.left = left;
        this.right = right;
      }
    }
    
    [Test]
    public void CompileNewStruct() {
      var create = Expression.Lambda<Func<EineStrukt2>>(
        Expression.New(typeof(EineStrukt2))).Interpret();
      
      var s = create();
      Assert.AreEqual(0, s.left);
      Assert.AreEqual(0, s.right);
    }
    
    [Test]
    public void CompileNewStructWithParameters() {
      var pl = Expression.Parameter(typeof(int), "left");
      var pr = Expression.Parameter(typeof(int), "right");
      
      var create = Expression.Lambda<Func<int, int, EineStrukt2>>(
        Expression.New(typeof(EineStrukt2).GetConstructor(new [] {
        typeof(int),
        typeof(int)
      }), pl, pr), pl, pr).Interpret();
      
      var s = create(42, 12);
      
      Assert.AreEqual(42, s.left);
      Assert.AreEqual(12, s.right);
    }

    public class EineKlass {
      
      public string Left { get; set; }

      public string Right { get; set; }
      
      public EineKlass() {
      }
      
      public EineKlass(string l, string r) {
        Left = l;
        Right = r;
      }
    }
    
    [Test]
    public void CompileNewClassEmptyConstructor() {
      var create = Expression.Lambda<Func<EineKlass>>(
        Expression.New(typeof(EineKlass))).Interpret();
      
      var k = create();
      Assert.IsNull(k.Left);
      Assert.IsNull(k.Right);
    }
    
    [Test]
    public void CompileNewClassWithParameters() {
      var pl = Expression.Parameter(typeof(string), "left");
      var pr = Expression.Parameter(typeof(string), "right");
      
      var create = Expression.Lambda<Func<string, string, EineKlass>>(
        Expression.New(typeof(EineKlass).GetConstructor(new [] {
        typeof(string),
        typeof(string)
      }), pl, pr), pl, pr).Interpret();
      
      var k = create("foo", "bar");
      
      Assert.AreEqual("foo", k.Left);
      Assert.AreEqual("bar", k.Right);
    }

    static Func<object> CreateNewArrayFactory<T>(params int [] bounds) {
      return Expression.Lambda<Func<object>>(
        Expression.NewArrayBounds(
        typeof(T),
        (from bound in bounds select bound.ToConstant()).ToArray())).Interpret();
    }

    [Test]
    public void CompileNewArraySingleDimensional() {
      var factory = CreateNewArrayFactory<int>(3);
      
      var array = (int[])factory();
      var type = array.GetType();
      
      Assert.IsNotNull(array);
      Assert.AreEqual(3, array.Length);
      Assert.IsTrue(type.IsArray);
      Assert.AreEqual(1, type.GetArrayRank());
    }
    
    [Test]
    public void CompileNewArrayMultiDimensional() {
      var factory = CreateNewArrayFactory<int>(3, 3);
      
      var array = (int[,])factory();
      var type = array.GetType();
      
      Assert.IsNotNull(array);
      Assert.IsTrue(type.IsArray);
      Assert.AreEqual(2, type.GetArrayRank());
      Assert.AreEqual(9, array.Length);
    }

    static Func<T[]> CreateArrayInit<T>(T[] ts) {
      return Expression.Lambda<Func<T[]>>(
        Expression.NewArrayInit(
        typeof(T),
        (from t in ts select t.ToConstant()).ToArray())).Interpret();
    }

    static void AssertCreatedArrayIsEqual<T>(params T[] ts) {
      var creator = CreateArrayInit(ts);
      var array = creator();
      
      Assert.IsTrue(ts.SequenceEqual(array));
    }
    
    [Test]
    public void CompileInitArrayOfInt() {
      AssertCreatedArrayIsEqual(new int[] { 1, 2, 3, 4 });
    }

    [Test]
    public void CompileInitArrayOfEnums() {
      AssertCreatedArrayIsEqual(new Months [] {
        Months.Jan,
        Months.Feb,
        Months.Mar,
        Months.Apr
      });
    }

    [Test]
    public void CompileInitArrayOfClasses() {
      AssertCreatedArrayIsEqual(new Foo[] {
        new Foo(),
        new Foo(),
        new Foo(),
        new Foo()
      });
    }

    struct Bar5 {
      public int bar;

      public Bar5(int b) {
        bar = b;
      }
    }
    
    [Test]
    public void CompileInitArrayOfStructs() {
      AssertCreatedArrayIsEqual(new Bar5 [] {
        new Bar5(1),
        new Bar5(2),
        new Bar5(3),
        new Bar5(4)
      });
    }

    [Test]
    public void CompileNotInt32() {
      var p = Expression.Parameter(typeof(int), "i");
      var not = Expression.Lambda<Func<int, int>>(Expression.Not(p), p).Interpret();
      
      Assert.AreEqual(-2, not(1));
      Assert.AreEqual(-4, not(3));
      Assert.AreEqual(2, not(-3));
    }
    
    [Test]
    public void CompiledNotNullableInt32() {
      var p = Expression.Parameter(typeof(int?), "i");
      var not = Expression.Lambda<Func<int?, int?>>(Expression.Not(p), p).Interpret();
      
      Assert.AreEqual(null, not(null));
      Assert.AreEqual((int?)-4, not(3));
      Assert.AreEqual((int?)2, not(-3));
    }
    
    [Test]
    public void CompileNotBool() {
      var p = Expression.Parameter(typeof(bool), "i");
      var not = Expression.Lambda<Func<bool, bool>>(Expression.Not(p), p).Interpret();
      
      Assert.AreEqual(false, not(true));
      Assert.AreEqual(true, not(false));
    }
    
    [Test]
    public void CompiledNotNullableBool() {
      var p = Expression.Parameter(typeof(bool?), "i");
      var not = Expression.Lambda<Func<bool?, bool?>>(Expression.Not(p), p).Interpret();
      
      Assert.AreEqual((bool?)null, not(null));
      Assert.AreEqual((bool?)false, not(true));
      Assert.AreEqual((bool?)true, not(false));
    }

    [Test]
    public void UserDefinedNotNullable() {
      var s = Expression.Parameter(typeof(Slot?), "s");
      var node = Expression.Not(s);
      Assert.IsTrue(node.IsLifted);
      Assert.IsTrue(node.IsLiftedToNull);
      Assert.AreEqual(typeof(bool?), node.Type);
      Assert.AreEqual(typeof(Slot).GetMethod("op_LogicalNot"), node.Method);
      
      var not = Expression.Lambda<Func<Slot?, bool?>>(node, s).Interpret();
      
      Assert.AreEqual(null, not(null));
      Assert.AreEqual(true, not(new Slot(1)));
      Assert.AreEqual(false, not(new Slot(0)));
    }

    [Test]
    public void NullableInt32NotEqual() {
      var l = Expression.Parameter(typeof(int?), "l");
      var r = Expression.Parameter(typeof(int?), "r");
      
      var neq = Expression.Lambda<Func<int?, int?, bool>>(
        Expression.NotEqual(l, r), l, r).Interpret();
      
      Assert.IsFalse(neq(null, null));
      Assert.IsTrue(neq(null, 1));
      Assert.IsTrue(neq(1, null));
      Assert.IsTrue(neq(1, 2));
      Assert.IsFalse(neq(1, 1));
      Assert.IsTrue(neq(null, 0));
      Assert.IsTrue(neq(0, null));
    }
    
    [Test]
    public void NullableInt32NotEqualLiftedToNull() {
      var l = Expression.Parameter(typeof(int?), "l");
      var r = Expression.Parameter(typeof(int?), "r");
      
      var neq = Expression.Lambda<Func<int?, int?, bool?>>(
        Expression.NotEqual(l, r, true, null), l, r).Interpret();
      
      Assert.AreEqual((bool?)null, neq(null, null));
      Assert.AreEqual((bool?)null, neq(null, 1));
      Assert.AreEqual((bool?)null, neq(1, null));
      Assert.AreEqual((bool?)true, neq(1, 2));
      Assert.AreEqual((bool?)false, neq(1, 1));
      Assert.AreEqual((bool?)null, neq(null, 0));
      Assert.AreEqual((bool?)null, neq(0, null));
    }

    [Test]
    public void EnumNotEqual() {
      var l = Expression.Parameter(typeof(FooEnum), "l");
      var r = Expression.Parameter(typeof(FooEnum), "r");
      
      var node = Expression.NotEqual(l, r);
      Assert.IsFalse(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(bool), node.Type);
      Assert.IsNull(node.Method);
      
      var neq = Expression.Lambda<Func<FooEnum, FooEnum, bool>>(node, l, r).Interpret();
      
      Assert.AreEqual(false, neq(FooEnum.Bar, FooEnum.Bar));
      Assert.AreEqual(true, neq(FooEnum.Bar, FooEnum.Baz));
    }
    
    [Test]
    public void LiftedEnumNotEqual() {
      var l = Expression.Parameter(typeof(FooEnum?), "l");
      var r = Expression.Parameter(typeof(FooEnum?), "r");
      
      var node = Expression.NotEqual(l, r);
      Assert.IsTrue(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(typeof(bool), node.Type);
      Assert.IsNull(node.Method);
      
      var neq = Expression.Lambda<Func<FooEnum?, FooEnum?, bool>>(node, l, r).Interpret();
      
      Assert.AreEqual(false, neq(FooEnum.Bar, FooEnum.Bar));
      Assert.AreEqual(true, neq(FooEnum.Bar, FooEnum.Baz));
      Assert.AreEqual(true, neq(FooEnum.Bar, null));
      Assert.AreEqual(false, neq(null, null));
    }

    [Test]
    public void OrBoolTest() {
      var a = Expression.Parameter(typeof(bool), "a");
      var b = Expression.Parameter(typeof(bool), "b");
      var l = Expression.Lambda<Func<bool, bool, bool>>(
        Expression.Or(a, b), a, b);
      
      var be = l.Body as BinaryExpression;
      Assert.IsNotNull(be);
      Assert.AreEqual(typeof(bool), be.Type);
      Assert.IsFalse(be.IsLifted);
      Assert.IsFalse(be.IsLiftedToNull);
      
      var c = l.Interpret();
      
      Assert.AreEqual(true, c(true, true), "o1");
      Assert.AreEqual(true, c(true, false), "o2");
      Assert.AreEqual(true, c(false, true), "o3");
      Assert.AreEqual(false, c(false, false), "o4");
    }
    
    [Test]
    public void OrBoolNullableTest() {
      var a = Expression.Parameter(typeof(bool?), "a");
      var b = Expression.Parameter(typeof(bool?), "b");
      var l = Expression.Lambda<Func<bool?, bool?, bool?>>(
        Expression.Or(a, b), a, b);
      
      var be = l.Body as BinaryExpression;
      Assert.IsNotNull(be);
      Assert.AreEqual(typeof(bool?), be.Type);
      Assert.IsTrue(be.IsLifted);
      Assert.IsTrue(be.IsLiftedToNull);
      
      var c = l.Interpret();
      
      Assert.AreEqual(true, c(true, true), "o1");
      Assert.AreEqual(true, c(true, false), "o2");
      Assert.AreEqual(true, c(false, true), "o3");
      Assert.AreEqual(false, c(false, false), "o4");
      
      Assert.AreEqual(true, c(true, null), "o5");
      Assert.AreEqual(null, c(false, null), "o6");
      Assert.AreEqual(null, c(null, false), "o7");
      Assert.AreEqual(true, c(true, null), "o8");
      Assert.AreEqual(null, c(null, null), "o9");
    }

    [Test]
    public void OrBoolItem() {
      var i = Expression.Parameter(typeof(Item<bool>), "i");
      var and = Expression.Lambda<Func<Item<bool>, bool>>(
        Expression.Or(
        Expression.Property(i, "Left"),
        Expression.Property(i, "Right")), i).Interpret();
      
      var item = new Item<bool>(true, false);
      Assert.AreEqual(true, and(item));
      Assert.IsTrue(item.LeftCalled);
      Assert.IsTrue(item.RightCalled);
    }
    
    [Test]
    public void OrNullableBoolItem() {
      var i = Expression.Parameter(typeof(Item<bool?>), "i");
      var and = Expression.Lambda<Func<Item<bool?>, bool?>>(
        Expression.Or(
        Expression.Property(i, "Left"),
        Expression.Property(i, "Right")), i).Interpret();
      
      var item = new Item<bool?>(true, false);
      Assert.AreEqual((bool?)true, and(item));
      Assert.IsTrue(item.LeftCalled);
      Assert.IsTrue(item.RightCalled);
    }
    
    [Test]
    public void OrIntTest() {
      var a = Expression.Parameter(typeof(int), "a");
      var b = Expression.Parameter(typeof(int), "b");
      var or = Expression.Lambda<Func<int, int, int>>(
        Expression.Or(a, b), a, b).Interpret();
      
      Assert.AreEqual((int?)1, or(1, 1), "o1");
      Assert.AreEqual((int?)1, or(1, 0), "o2");
      Assert.AreEqual((int?)1, or(0, 1), "o3");
      Assert.AreEqual((int?)0, or(0, 0), "o4");
    }
    
    [Test]
    public void OrIntNullableTest() {
      var a = Expression.Parameter(typeof(int?), "a");
      var b = Expression.Parameter(typeof(int?), "b");
      var c = Expression.Lambda<Func<int?, int?, int?>>(
        Expression.Or(a, b), a, b).Interpret();
      
      Assert.AreEqual((int?)1, c(1, 1), "o1");
      Assert.AreEqual((int?)1, c(1, 0), "o2");
      Assert.AreEqual((int?)1, c(0, 1), "o3");
      Assert.AreEqual((int?)0, c(0, 0), "o4");
      
      Assert.AreEqual((int?)null, c(1, null), "o5");
      Assert.AreEqual((int?)null, c(0, null), "o6");
      Assert.AreEqual((int?)null, c(null, 0), "o7");
      Assert.AreEqual((int?)null, c(1, null), "o8");
      Assert.AreEqual((int?)null, c(null, null), "o9");
    }

    [Test]
    public void OrElseTest() {
      var a = Expression.Parameter(typeof(bool), "a");
      var b = Expression.Parameter(typeof(bool), "b");
      var l = Expression.Lambda<Func<bool, bool, bool>>(
        Expression.OrElse(a, b), a, b);
      
      var be = l.Body as BinaryExpression;
      Assert.IsNotNull(be);
      Assert.AreEqual(typeof(bool), be.Type);
      Assert.IsFalse(be.IsLifted);
      Assert.IsFalse(be.IsLiftedToNull);
      
      var c = l.Interpret();
      
      Assert.AreEqual(true, c(true, true), "o1");
      Assert.AreEqual(true, c(true, false), "o2");
      Assert.AreEqual(true, c(false, true), "o3");
      Assert.AreEqual(false, c(false, false), "o4");
    }

    [Test]
    public void OrElseTestNullable() {
      var a = Expression.Parameter(typeof(bool?), "a");
      var b = Expression.Parameter(typeof(bool?), "b");
      var l = Expression.Lambda<Func<bool?, bool?, bool?>>(
        Expression.OrElse(a, b), a, b);
      
      var be = l.Body as BinaryExpression;
      Assert.IsNotNull(be);
      Assert.AreEqual(typeof(bool?), be.Type);
      Assert.IsTrue(be.IsLifted);
      Assert.IsTrue(be.IsLiftedToNull);
      
      var c = l.Interpret();
      
      Assert.AreEqual(true, c(true, true), "o1");
      Assert.AreEqual(true, c(true, false), "o2");
      Assert.AreEqual(true, c(false, true), "o3");
      Assert.AreEqual(false, c(false, false), "o4");
      
      Assert.AreEqual(true, c(true, null), "o5");
      Assert.AreEqual(null, c(false, null), "o6");
      Assert.AreEqual(null, c(null, false), "o7");
      Assert.AreEqual(true, c(true, null), "o8");
      Assert.AreEqual(null, c(null, null), "o9");
    }
    
    [Test]
    public void OrElseBoolItem() {
      var i = Expression.Parameter(typeof(Item<bool>), "i");
      var and = Expression.Lambda<Func<Item<bool>, bool>>(
        Expression.OrElse(
        Expression.Property(i, "Left"),
        Expression.Property(i, "Right")), i).Interpret();
      
      var item = new Item<bool>(true, false);
      Assert.AreEqual(true, and(item));
      Assert.IsTrue(item.LeftCalled);
      Assert.IsFalse(item.RightCalled);
    }
    
    [Test]
    public void OrElseNullableBoolItem() {
      var i = Expression.Parameter(typeof(Item<bool?>), "i");
      var and = Expression.Lambda<Func<Item<bool?>, bool?>>(
        Expression.OrElse(
        Expression.Property(i, "Left"),
        Expression.Property(i, "Right")), i).Interpret();
      
      var item = new Item<bool?>(true, false);
      Assert.AreEqual((bool?)true, and(item));
      Assert.IsTrue(item.LeftCalled);
      Assert.IsFalse(item.RightCalled);
    }

    [Test]
    public void UserDefinedOrElse() {
      var l = Expression.Parameter(typeof(Slot), "l");
      var r = Expression.Parameter(typeof(Slot), "r");
      
      var method = typeof(Slot).GetMethod("op_BitwiseOr");
      
      var node = Expression.OrElse(l, r, method);
      Assert.IsFalse(node.IsLifted);
      Assert.IsFalse(node.IsLiftedToNull);
      Assert.AreEqual(method, node.Method);
      
      var orelse = Expression.Lambda<Func<Slot, Slot, Slot>>(node, l, r).Interpret();
      
      Assert.AreEqual(new Slot(64), orelse(new Slot(64), new Slot(64)));
      Assert.AreEqual(new Slot(32), orelse(new Slot(32), new Slot(64)));
    }

    [Test]
    public void UserDefinedOrElseLiftedToNull() {
      var l = Expression.Parameter(typeof(Slot?), "l");
      var r = Expression.Parameter(typeof(Slot?), "r");
      
      var method = typeof(Slot).GetMethod("op_BitwiseOr");
      
      var node = Expression.OrElse(l, r, method);
      Assert.IsTrue(node.IsLifted);
      Assert.IsTrue(node.IsLiftedToNull);
      Assert.AreEqual(method, node.Method);
      
      var orelse = Expression.Lambda<Func<Slot?, Slot?, Slot?>>(node, l, r).Interpret();
      
      Assert.AreEqual(new Slot(64), orelse(new Slot(64), new Slot(64)));
      Assert.AreEqual(new Slot(32), orelse(new Slot(32), new Slot(64)));
      Assert.AreEqual(new Slot(64), orelse(null, new Slot(64)));
      Assert.AreEqual(new Slot(32), orelse(new Slot(32), null));
      Assert.AreEqual(null, orelse(null, null));
    }

    [Test]
    public void UserDefinedOrElseShortCircuit() {
      var i = Expression.Parameter(typeof(Item<Slot>), "i");
      var orelse = Expression.Lambda<Func<Item<Slot>, Slot>>(
        Expression.OrElse(
        Expression.Property(i, "Left"),
        Expression.Property(i, "Right")), i).Interpret();
      
      var item = new Item<Slot>(new Slot(1), new Slot(0));
      Assert.AreEqual(new Slot(1), orelse(item));
      Assert.IsTrue(item.LeftCalled);
      Assert.IsFalse(item.RightCalled);
    }
    
    [Test]
    [Category ("NotDotNet")]
    // https://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=350228
    public void UserDefinedLiftedOrElseShortCircuit() {
      var i = Expression.Parameter(typeof(Item<Slot?>), "i");
      var orelse = Expression.Lambda<Func<Item<Slot?>, Slot?>>(
        Expression.OrElse(
        Expression.Property(i, "Left"),
        Expression.Property(i, "Right")), i).Interpret();
      
      var item = new Item<Slot?>(new Slot(1), null);
      Assert.AreEqual((Slot?)new Slot(1), orelse(item));
      Assert.IsTrue(item.LeftCalled);
      Assert.IsFalse(item.RightCalled);
    }

    public class Foo5 {
      public string Prop { get; set; }
      
      public static string StatProp {
        get { return "StaticFoo"; }
      }
    }
    
    [Test]
    public void TestCompileGetInstanceProperty() {
      var p = Expression.Parameter(typeof(Foo5), "foo");
      var fooer = Expression.Lambda<Func<Foo5, string>>(
        Expression.Property(p, typeof(Foo5).GetProperty("Prop")), p).Interpret();
      
      Assert.AreEqual("foo", fooer(new Foo5 { Prop = "foo" }));
    }
    
    [Test]
    public void TestCompileGetStaticProperty() {
      var sf = Expression.Lambda<Func<string>>(
        Expression.Property(null, typeof(Foo5).GetProperty(
        "StatProp", BindingFlags.Public | BindingFlags.Static))).Interpret();
      
      Assert.AreEqual("StaticFoo", sf());
    }

    public struct Bar6 {
      private string slot;
      
      public string Prop {
        get { return slot; }
        set { slot = value; }
      }
      
      public Bar6(string slot) {
        this.slot = slot;
      }
    }
    
    [Test]
    public void TestCompileGetInstancePropertyOnStruct() {
      var p = Expression.Parameter(typeof(Bar6), "bar");
      var barer = Expression.Lambda<Func<Bar6, string>>(
        Expression.Property(p, typeof(Bar6).GetProperty("Prop")), p).Interpret();
      
      Assert.AreEqual("bar", barer(new Bar6("bar")));
    }

    [Test]
    public void CompiledQuote() {
      var quote42 = Expression.Lambda<Func<Expression<Func<int>>>>(
        Expression.Quote(
        Expression.Lambda<Func<int>>(
        42.ToConstant()))).Interpret();
      
      var get42 = quote42().Interpret();
      
      Assert.AreEqual(42, get42());
    }
    
    [Test]
    public void ParameterInQuotedExpression() { // #550722
      // Expression<Func<string, Expression<Func<string>>>> e = (string s) => () => s;

      var s = Expression.Parameter(typeof(string), "s");

      var lambda = Expression.Lambda<Func<string, Expression<Func<string>>>>(
        Expression.Quote(
        Expression.Lambda<Func<string>>(s, new ParameterExpression [0])),
        s);

      var fs = lambda.Interpret()("bingo").Interpret();

      Assert.AreEqual("bingo", fs());
    }

    [Test]
    public void ParameterInQuotedExpressionSyntax() { // #550722
      Expression<Func<string, Expression<Func<string>>>> lambda = (string s) => () => s;

      var fs = lambda.Interpret()("bingo").Interpret();

      Assert.AreEqual("bingo", fs());
    }

    [Test]
    public void CompileRightShift() {
      var l = Expression.Parameter(typeof(int), "l");
      var r = Expression.Parameter(typeof(int), "r");
      
      var rs = Expression.Lambda<Func<int, int, int>>(
        Expression.RightShift(l, r), l, r).Interpret();
      
      Assert.AreEqual(3, rs(6, 1));
      Assert.AreEqual(1, rs(12, 3));
    }
    
    [Test]
    public void RightShiftNullableLongAndInt() {
      var l = Expression.Parameter(typeof(long?), "l");
      var r = Expression.Parameter(typeof(int), "r");
      
      var node = Expression.RightShift(l, r);
      Assert.IsTrue(node.IsLifted);
      Assert.IsTrue(node.IsLiftedToNull);
      Assert.AreEqual(typeof(long?), node.Type);
      
      var rs = Expression.Lambda<Func<long?, int, long?>>(node, l, r).Interpret();
      
      Assert.AreEqual(null, rs(null, 2));
      Assert.AreEqual(512, rs(1024, 1));
    }

    //
    // This method makes sure that compiling an AddChecked on two values
    // throws an OverflowException, if it doesnt, it fails
    //
    static void MustOverflowSubtract<T>(T v1, T v2) {
      Expression<Func<T>> l = Expression.Lambda<Func<T>>(
        Expression.SubtractChecked(Expression.Constant(v1), Expression.Constant(v2)));
      Func<T> del = l.Interpret();
      T res = default (T);
      try {
        res = del();
      } catch (OverflowException) {
        // OK
        return;
      }
      throw new Exception(String.Format("SubtractChecked on {2} should have thrown an exception with values {0} {1}, result was: {3}",
                                          v1, v2, v1.GetType(), res));
    }
    
    //
    // This routine should execute the code, but not throw an
    // overflow exception
    //
    static void MustNotOverflowSubtract<T>(T v1, T v2) {
      Expression<Func<T>> l = Expression.Lambda<Func<T>>(
        Expression.SubtractChecked(Expression.Constant(v1), Expression.Constant(v2)));
      Func<T> del = l.Interpret();
      del();
    }

    static void InvalidOperationSubtract<T>(T v1, T v2) {
      try {
        Expression.Lambda<Func<T>>(
          Expression.SubtractChecked(Expression.Constant(v1), Expression.Constant(v2)));
      } catch (InvalidOperationException) {
        // OK
        return;
      }
      throw new Exception(String.Format("SubtractChecked should have thrown for the creation of a tree with {0} operands", v1.GetType()));
    }

    [Test]
    public void TestSubtractOverflows() {
      // These should overflow, check the various types and codepaths
      // in BinaryExpression:
      MustOverflowSubtract<int>(Int32.MinValue, 1);
      MustOverflowSubtract<int>(Int32.MaxValue, -1);
      MustOverflowSubtract<long>(Int64.MinValue, 1);
      MustOverflowSubtract<long>(Int64.MaxValue, -1);
      
      MustOverflowSubtract<ushort>(UInt16.MinValue, 1);
      
      // unsigned values use Sub_Ovf_Un, check that too:
      MustOverflowSubtract<ulong>(0, 1);
      MustOverflowSubtract<uint>(0, 1);
    }

    [Test]
    public void TestNoOverflowSubtract() {
      // Simple stuff
      MustNotOverflowSubtract<int>(10, 20);
      
      // There are invalid:
      InvalidOperationSubtract<byte>(Byte.MinValue, 1);
      InvalidOperationSubtract<sbyte>(SByte.MaxValue, 2);
      
      MustNotOverflowSubtract<short>(Int16.MaxValue, 2);
      MustNotOverflowSubtract<ushort>(UInt16.MaxValue, 2);
      
      // Doubles, floats, do not overflow
      MustNotOverflowSubtract<float>(Single.MaxValue, 1);
      MustNotOverflowSubtract<double>(Double.MaxValue, 1);
    }

    static Func<object, TType> CreateTypeAs<TType>() {
      var obj = Expression.Parameter(typeof(object), "obj");
      
      return Expression.Lambda<Func<object, TType>>(
        Expression.TypeAs(obj, typeof(TType)), obj).Interpret();
    }

    struct Foo7 {
    }
    
    class Bar7 {
    }
    
    class Baz7 : Bar7 {
    }
    
    [Test]
    public void CompiledTypeAs() {
      var asbar = CreateTypeAs<Bar7>();
      var asbaz = CreateTypeAs<Baz7>();
      
      Assert.IsNotNull(asbar(new Bar7()));
      Assert.IsNull(asbar(new Foo7()));
      Assert.IsNotNull(asbar(new Baz7()));
      Assert.IsNull(asbaz(new Bar7()));
    }
    
    [Test]
    [ExpectedException (typeof (ArgumentException))]
    public void TypeAsVoid() {
      Expression.TypeAs("yoyo".ToConstant(), typeof(void));
    }

    static Func<TType, bool> CreateTypeIs<TType, TCandidate>() {
      var p = Expression.Parameter(typeof(TType), "p");
      
      return Expression.Lambda<Func<TType, bool>>(
        Expression.TypeIs(p, typeof(TCandidate)), p).Interpret();
    }

    [Test]
    public void CompiledTypeIs() {
      var foo_is_bar = CreateTypeIs<Foo7, Bar7>();
      var foo_is_foo = CreateTypeIs<Foo7, Foo7>();
      var bar_is_bar = CreateTypeIs<Bar7, Bar7>();
      var bar_is_foo = CreateTypeIs<Bar7, Foo7>();
      var baz_is_bar = CreateTypeIs<Baz7, Bar7>();
      
      Assert.IsTrue(foo_is_foo(new Foo7()));
      Assert.IsFalse(foo_is_bar(new Foo7()));
      Assert.IsTrue(bar_is_bar(new Bar7()));
      Assert.IsFalse(bar_is_foo(new Bar7()));
      Assert.IsTrue(baz_is_bar(new Baz7()));
    }

    public static void TacTac() {
    }
    
    [Test]
    public void VoidIsObject() {
      var vio = Expression.Lambda<Func<bool>>(
        Expression.TypeIs(
        Expression.Call(GetType().GetMethod("TacTac")),
        typeof(object))).Interpret();
      
      Assert.IsFalse(vio());
    }

    [Test]
    public void CompilePlusInt32() {
      var p = Expression.Parameter(typeof(int), "i");
      var plus = Expression.Lambda<Func<int, int>>(Expression.UnaryPlus(p), p).Interpret();
      
      Assert.AreEqual(-2, plus(-2));
      Assert.AreEqual(0, plus(0));
      Assert.AreEqual(3, plus(3));
    }

    private int DoStuff() {
      return 1337;
    }

    [Test]
    public void CallPrivateMethod() {
      Expression<Func<int>> doStuffExpr = () => DoStuff();
      Assert.AreEqual(1337, doStuffExpr.Interpret()());
    }
  }
}

