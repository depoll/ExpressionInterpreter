using System;
using System.Reflection;
using System.Linq.Expressions;
using NUnit.Framework;

namespace Tests {
  public class OpClass {
    public static OpClass operator +(OpClass a, OpClass b) {
      return a;
    }
    
    public static OpClass operator -(OpClass a, OpClass b) {
      return a;
    }
    
    public static OpClass operator *(OpClass a, OpClass b) {
      return a;
    }
    
    public static OpClass operator /(OpClass a, OpClass b) {
      return a;
    }
    
    public static OpClass operator %(OpClass a, OpClass b) {
      return a;
    }
    
    public static OpClass operator &(OpClass a, OpClass b) {
      return a;
    }
    
    public static OpClass operator |(OpClass a, OpClass b) {
      return a;
    }
    
    public static OpClass operator ^(OpClass a, OpClass b) {
      return a;
    }
    
    public static OpClass operator >>(OpClass a, int b) {
      return a;
    }
    
    public static OpClass operator <<(OpClass a, int b) {
      return a;
    }
    
    public static bool operator true(OpClass a) {
      return false;
    }
    
    public static bool operator false(OpClass a) {
      return false;
    }
    
    public static bool operator >(OpClass a, OpClass b) {
      return false;
    }
    
    public static bool operator <(OpClass a, OpClass b) {
      return false;
    }
    
    public static bool operator >=(OpClass a, OpClass b) {
      return false;
    }
    
    public static bool operator <=(OpClass a, OpClass b) {
      return false;
    }
    
    public static OpClass operator +(OpClass a) {
      return a;
    }
    
    public static OpClass operator -(OpClass a) {
      return a;
    }
    
    public static OpClass operator !(OpClass a) {
      return a;
    }
    
    public static OpClass operator ~(OpClass a) {
      return a;
    }
    
    public static void WrongUnaryReturnVoid(OpClass a) {
    }
    
    public static OpClass WrongUnaryParameterCount(OpClass a, OpClass b) {
      return a;
    }
    
    public OpClass WrongUnaryNotStatic(OpClass a) {
      return a;
    }
    
    public static bool operator ==(OpClass a, OpClass b) {
      return ((object)a) == ((object)b);
    }
    
    public static bool operator !=(OpClass a, OpClass b) {
      return ((object)a) != ((object)b);
    }
    
    //
    // Required when you have == or !=
    //
    public override bool Equals(object o) {
      return ((object)this) == o;
    }
    
    public override int GetHashCode() {
      return 1;
    }
  }
  
  public class NoOpClass {
    // No user-defined operators here (we use this class to test for exceptions.)
  }
  
  public class MemberClass {
    public int TestField1 = 0;
    public readonly int TestField2 = 1;

    public int TestProperty1 { get { return TestField1; } }

    public int TestProperty2 { get { return TestField1; } set { TestField1 = value; } }

    public int TestMethod(int i) {
      return TestField1 + i;
    }

    public T TestGenericMethod<T>(T arg) {
      return arg;
    }
    
    public delegate int TestDelegate(int i);

    public event TestDelegate TestEvent;
    
    public void DoNothing() {
      // Just to avoid a compiler warning
      if (TestEvent != null) {
        return;
      }
    }
    
    public static int StaticField = 0;

    public static int StaticProperty { get { return StaticField; } set { StaticField = value; } }

    public static int StaticMethod(int i) {
      return 1 + i;
    }

    public static T StaticGenericMethod<T>(T arg) {
      return arg;
    }
    
    public static MethodInfo GetMethodInfo() {
      return typeof(MemberClass).GetMethod("TestMethod");
    }
    
    public static FieldInfo GetRoFieldInfo() {
      return typeof(MemberClass).GetField("TestField1");
    }
    
    public static FieldInfo GetRwFieldInfo() {
      return typeof(MemberClass).GetField("TestField2");
    }
    
    public static PropertyInfo GetRoPropertyInfo() {
      return typeof(MemberClass).GetProperty("TestProperty1");
    }
    
    public static PropertyInfo GetRwPropertyInfo() {
      return typeof(MemberClass).GetProperty("TestProperty2");
    }
    
    public static EventInfo GetEventInfo() {
      return typeof(MemberClass).GetEvent("TestEvent");
    }
    
    public static FieldInfo GetStaticFieldInfo() {
      return typeof(MemberClass).GetField("StaticField");
    }
    
    public static PropertyInfo GetStaticPropertyInfo() {
      return typeof(MemberClass).GetProperty("StaticProperty");
    }
    
  }
  
  public struct OpStruct {
    public static OpStruct operator +(OpStruct a, OpStruct b) {
      return a;
    }
    
    public static OpStruct operator -(OpStruct a, OpStruct b) {
      return a;
    }
    
    public static OpStruct operator *(OpStruct a, OpStruct b) {
      return a;
    }
    
    public static OpStruct operator &(OpStruct a, OpStruct b) {
      return a;
    }
  }
  
  static class ExpressionExtensions {
    
    public static ConstantExpression ToConstant<T>(this T t) {
      return Expression.Constant(t);
    }
    
    public static void AssertThrows(this Action action, Type type) {
      try {
        action();
        Assert.Fail();
      } catch (Exception e) {
        if (e.GetType() != type) {
          Assert.Fail();
        }
      }
    }
  }
  
  class Item<T> {
    
    bool left_called;
    T left;
    
    public T Left {
      get {
        left_called = true;
        return left;
      }
    }
    
    public bool LeftCalled {
      get { return left_called; }
    }
    
    bool right_called;
    T right;
    
    public T Right {
      get {
        right_called = true;
        return right;
      }
    }
    
    public bool RightCalled {
      get { return right_called; }
    }
    
    public Item(T left, T right) {
      this.left = left;
      this.right = right;
    }
  }
}

