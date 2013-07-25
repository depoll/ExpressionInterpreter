using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace ExpressionInterpreter {
  public static class ExpressionExtensions {
    private class Scope {
      private readonly Scope parent;
      private readonly Dictionary<ParameterExpression, object> values = new Dictionary<ParameterExpression, object>();

      public Scope() {
        parent = null;
      }

      public Scope(Scope parent) {
        this.parent = parent;
      }

      public bool HasParameter(ParameterExpression key) {
        return values.ContainsKey(key) || (parent != null && parent.HasParameter(key));
      }

      public object this[ParameterExpression key] {
        get {
          object result;
          if (values.TryGetValue(key, out result)) {
            return result;
          }
          if (parent != null) {
            return parent[key];
          }
          throw new InvalidOperationException("Parameter not defined.");
        }
        set {
          if (values.ContainsKey(key)) {
            values[key] = value;
            return;
          }
          if (parent != null) {
            parent[key] = value;
            return;
          }
          throw new KeyNotFoundException();
        }
      }

      public void Register(ParameterExpression expr, object value) {
        values[expr] = value;
      }
    }

    private class ParameterReplacer : ExpressionVisitor {
      private readonly Scope scope;

      public ParameterReplacer(Scope scope) {
        this.scope = scope;
      }

      protected override Expression VisitParameter(ParameterExpression expr) {
        if (scope.HasParameter(expr)) {
          var boxType = typeof(StrongBox<>).MakeGenericType(expr.Type);
          return Expression.Field(Expression.Constant(Activator.CreateInstance(boxType, scope[expr]), boxType), "Value");
        }
        return base.VisitParameter(expr);
      }
    }

    private class EvaluatingVisitor : ExpressionVisitor {
      private static readonly Dictionary<Tuple<Type, ExpressionType>, Func<Func<object>, Func<object>, object>> builtinConversions;

      static EvaluatingVisitor() {
        builtinConversions = new Dictionary<Tuple<Type, ExpressionType>, Func<Func<object>, Func<object>, object>> {
          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.Add), (left, right) => (sbyte)left() + (sbyte)right()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.Add), (left, right) => (byte)left() + (byte)right()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.Add), (left, right) => (short)left() + (short)right()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.Add), (left, right) => (ushort)left() + (ushort)right()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.Add), (left, right) => (int)left() + (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.Add), (left, right) => (uint)left() + (uint)right()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.Add), (left, right) => (long)left() + (long)right()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.Add), (left, right) => (char)left() + (char)right()},
          {new Tuple<Type, ExpressionType>(typeof(float), ExpressionType.Add), (left, right) => (float)left() + (float)right()},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.Add), (left, right) => (ulong)left() + (ulong)right()},
          {new Tuple<Type, ExpressionType>(typeof(double), ExpressionType.Add), (left, right) => (double)left() + (double)right()},
          {new Tuple<Type, ExpressionType>(typeof(decimal), ExpressionType.Add), (left, right) => (decimal)left() + (decimal)right()},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.AddChecked), (left, right) => checked((sbyte)left() + (sbyte)right())},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.AddChecked), (left, right) => checked((byte)left() + (byte)right())},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.AddChecked), (left, right) => checked((short)left() + (short)right())},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.AddChecked), (left, right) => checked((ushort)left() + (ushort)right())},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.AddChecked), (left, right) => checked((int)left() + (int)right())},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.AddChecked), (left, right) => checked((uint)left() + (uint)right())},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.AddChecked), (left, right) => checked((long)left() + (long)right())},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.AddChecked), (left, right) => checked((char)left() + (char)right())},
          {new Tuple<Type, ExpressionType>(typeof(float), ExpressionType.AddChecked), (left, right) => checked((float)left() + (float)right())},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.AddChecked), (left, right) => checked((ulong)left() + (ulong)right())},
          {new Tuple<Type, ExpressionType>(typeof(double), ExpressionType.AddChecked), (left, right) => checked((double)left() + (double)right())},
          {new Tuple<Type, ExpressionType>(typeof(decimal), ExpressionType.AddChecked), (left, right) => checked((decimal)left() + (decimal)right())},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.Subtract), (left, right) => (sbyte)left() - (sbyte)right()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.Subtract), (left, right) => (byte)left() - (byte)right()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.Subtract), (left, right) => (short)left() - (short)right()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.Subtract), (left, right) => (ushort)left() - (ushort)right()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.Subtract), (left, right) => (int)left() - (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.Subtract), (left, right) => (uint)left() - (uint)right()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.Subtract), (left, right) => (long)left() - (long)right()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.Subtract), (left, right) => (char)left() - (char)right()},
          {new Tuple<Type, ExpressionType>(typeof(float), ExpressionType.Subtract), (left, right) => (float)left() - (float)right()},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.Subtract), (left, right) => (ulong)left() - (ulong)right()},
          {new Tuple<Type, ExpressionType>(typeof(double), ExpressionType.Subtract), (left, right) => (double)left() - (double)right()},
          {new Tuple<Type, ExpressionType>(typeof(decimal), ExpressionType.Subtract), (left, right) => (decimal)left() - (decimal)right()},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.SubtractChecked), (left, right) => checked((sbyte)left() - (sbyte)right())},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.SubtractChecked), (left, right) => checked((byte)left() - (byte)right())},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.SubtractChecked), (left, right) => checked((short)left() - (short)right())},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.SubtractChecked), (left, right) => checked((ushort)left() - (ushort)right())},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.SubtractChecked), (left, right) => checked((int)left() - (int)right())},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.SubtractChecked), (left, right) => checked((uint)left() - (uint)right())},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.SubtractChecked), (left, right) => checked((long)left() - (long)right())},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.SubtractChecked), (left, right) => checked((char)left() - (char)right())},
          {new Tuple<Type, ExpressionType>(typeof(float), ExpressionType.SubtractChecked), (left, right) => checked((float)left() - (float)right())},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.SubtractChecked), (left, right) => checked((ulong)left() - (ulong)right())},
          {new Tuple<Type, ExpressionType>(typeof(double), ExpressionType.SubtractChecked), (left, right) => checked((double)left() - (double)right())},
          {new Tuple<Type, ExpressionType>(typeof(decimal), ExpressionType.SubtractChecked), (left, right) => checked((decimal)left() - (decimal)right())},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.Multiply), (left, right) => (sbyte)left() * (sbyte)right()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.Multiply), (left, right) => (byte)left() * (byte)right()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.Multiply), (left, right) => (short)left() * (short)right()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.Multiply), (left, right) => (ushort)left() * (ushort)right()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.Multiply), (left, right) => (int)left() * (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.Multiply), (left, right) => (uint)left() * (uint)right()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.Multiply), (left, right) => (long)left() * (long)right()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.Multiply), (left, right) => (char)left() * (char)right()},
          {new Tuple<Type, ExpressionType>(typeof(float), ExpressionType.Multiply), (left, right) => (float)left() * (float)right()},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.Multiply), (left, right) => (ulong)left() * (ulong)right()},
          {new Tuple<Type, ExpressionType>(typeof(double), ExpressionType.Multiply), (left, right) => (double)left() * (double)right()},
          {new Tuple<Type, ExpressionType>(typeof(decimal), ExpressionType.Multiply), (left, right) => (decimal)left() * (decimal)right()},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.MultiplyChecked), (left, right) => checked((sbyte)left() * (sbyte)right())},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.MultiplyChecked), (left, right) => checked((byte)left() * (byte)right())},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.MultiplyChecked), (left, right) => checked((short)left() * (short)right())},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.MultiplyChecked), (left, right) => checked((ushort)left() * (ushort)right())},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.MultiplyChecked), (left, right) => checked((int)left() * (int)right())},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.MultiplyChecked), (left, right) => checked((uint)left() * (uint)right())},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.MultiplyChecked), (left, right) => checked((long)left() * (long)right())},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.MultiplyChecked), (left, right) => checked((char)left() * (char)right())},
          {new Tuple<Type, ExpressionType>(typeof(float), ExpressionType.MultiplyChecked), (left, right) => checked((float)left() * (float)right())},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.MultiplyChecked), (left, right) => checked((ulong)left() * (ulong)right())},
          {new Tuple<Type, ExpressionType>(typeof(double), ExpressionType.MultiplyChecked), (left, right) => checked((double)left() * (double)right())},
          {new Tuple<Type, ExpressionType>(typeof(decimal), ExpressionType.MultiplyChecked), (left, right) => checked((decimal)left() * (decimal)right())},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.Divide), (left, right) => (sbyte)left() / (sbyte)right()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.Divide), (left, right) => (byte)left() / (byte)right()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.Divide), (left, right) => (short)left() / (short)right()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.Divide), (left, right) => (ushort)left() / (ushort)right()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.Divide), (left, right) => (int)left() / (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.Divide), (left, right) => (uint)left() / (uint)right()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.Divide), (left, right) => (long)left() / (long)right()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.Divide), (left, right) => (char)left() / (char)right()},
          {new Tuple<Type, ExpressionType>(typeof(float), ExpressionType.Divide), (left, right) => (float)left() / (float)right()},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.Divide), (left, right) => (ulong)left() / (ulong)right()},
          {new Tuple<Type, ExpressionType>(typeof(double), ExpressionType.Divide), (left, right) => (double)left() / (double)right()},
          {new Tuple<Type, ExpressionType>(typeof(decimal), ExpressionType.Divide), (left, right) => (decimal)left() / (decimal)right()},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.Modulo), (left, right) => (sbyte)left() % (sbyte)right()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.Modulo), (left, right) => (byte)left() % (byte)right()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.Modulo), (left, right) => (short)left() % (short)right()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.Modulo), (left, right) => (ushort)left() % (ushort)right()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.Modulo), (left, right) => (int)left() % (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.Modulo), (left, right) => (uint)left() % (uint)right()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.Modulo), (left, right) => (long)left() % (long)right()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.Modulo), (left, right) => (char)left() % (char)right()},
          {new Tuple<Type, ExpressionType>(typeof(float), ExpressionType.Modulo), (left, right) => (float)left() % (float)right()},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.Modulo), (left, right) => (ulong)left() % (ulong)right()},
          {new Tuple<Type, ExpressionType>(typeof(double), ExpressionType.Modulo), (left, right) => (double)left() % (double)right()},
          {new Tuple<Type, ExpressionType>(typeof(decimal), ExpressionType.Modulo), (left, right) => (decimal)left() % (decimal)right()},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.And), (left, right) => (sbyte)left() & (sbyte)right()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.And), (left, right) => (byte)left() & (byte)right()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.And), (left, right) => (short)left() & (short)right()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.And), (left, right) => (ushort)left() & (ushort)right()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.And), (left, right) => (int)left() & (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.And), (left, right) => (uint)left() & (uint)right()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.And), (left, right) => (long)left() & (long)right()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.And), (left, right) => (char)left() & (char)right()},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.And), (left, right) => (ulong)left() & (ulong)right()},
          {new Tuple<Type, ExpressionType>(typeof(bool), ExpressionType.And), (left, right) => (bool)left() & (bool)right()},
          
          {new Tuple<Type, ExpressionType>(typeof(bool), ExpressionType.AndAlso), (left, right) => (bool)left() && (bool)right()},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.Or), (left, right) => (sbyte)left() | (sbyte)right()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.Or), (left, right) => (byte)left() | (byte)right()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.Or), (left, right) => (short)left() | (short)right()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.Or), (left, right) => (ushort)left() | (ushort)right()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.Or), (left, right) => (int)left() | (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.Or), (left, right) => (uint)left() | (uint)right()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.Or), (left, right) => (long)left() | (long)right()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.Or), (left, right) => (char)left() | (char)right()},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.Or), (left, right) => (ulong)left() | (ulong)right()},
          {new Tuple<Type, ExpressionType>(typeof(bool), ExpressionType.Or), (left, right) => (bool)left() | (bool)right()},
          
          {new Tuple<Type, ExpressionType>(typeof(bool), ExpressionType.OrElse), (left, right) => (bool)left() || (bool)right()},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.ExclusiveOr), (left, right) => (sbyte)left() ^ (sbyte)right()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.ExclusiveOr), (left, right) => (byte)left() ^ (byte)right()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.ExclusiveOr), (left, right) => (short)left() ^ (short)right()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.ExclusiveOr), (left, right) => (ushort)left() ^ (ushort)right()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.ExclusiveOr), (left, right) => (int)left() ^ (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.ExclusiveOr), (left, right) => (uint)left() ^ (uint)right()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.ExclusiveOr), (left, right) => (long)left() ^ (long)right()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.ExclusiveOr), (left, right) => (char)left() ^ (char)right()},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.ExclusiveOr), (left, right) => (ulong)left() ^ (ulong)right()},
          {new Tuple<Type, ExpressionType>(typeof(bool), ExpressionType.ExclusiveOr), (left, right) => (bool)left() ^ (bool)right()},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.Equal), (left, right) => (sbyte)left() == (sbyte)right()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.Equal), (left, right) => (byte)left() == (byte)right()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.Equal), (left, right) => (short)left() == (short)right()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.Equal), (left, right) => (ushort)left() == (ushort)right()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.Equal), (left, right) => (int)left() == (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.Equal), (left, right) => (uint)left() == (uint)right()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.Equal), (left, right) => (long)left() == (long)right()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.Equal), (left, right) => (char)left() == (char)right()},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.Equal), (left, right) => (ulong)left() == (ulong)right()},
          {new Tuple<Type, ExpressionType>(typeof(bool), ExpressionType.Equal), (left, right) => (bool)left() == (bool)right()},
          {new Tuple<Type, ExpressionType>(typeof(float), ExpressionType.Equal), (left, right) => (float)left() == (float)right()},
          {new Tuple<Type, ExpressionType>(typeof(double), ExpressionType.Equal), (left, right) => (double)left() == (double)right()},
          {new Tuple<Type, ExpressionType>(typeof(decimal), ExpressionType.Equal), (left, right) => (decimal)left() == (decimal)right()},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.NotEqual), (left, right) => (sbyte)left() != (sbyte)right()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.NotEqual), (left, right) => (byte)left() != (byte)right()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.NotEqual), (left, right) => (short)left() != (short)right()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.NotEqual), (left, right) => (ushort)left() != (ushort)right()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.NotEqual), (left, right) => (int)left() != (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.NotEqual), (left, right) => (uint)left() != (uint)right()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.NotEqual), (left, right) => (long)left() != (long)right()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.NotEqual), (left, right) => (char)left() != (char)right()},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.NotEqual), (left, right) => (ulong)left() != (ulong)right()},
          {new Tuple<Type, ExpressionType>(typeof(bool), ExpressionType.NotEqual), (left, right) => (bool)left() != (bool)right()},
          {new Tuple<Type, ExpressionType>(typeof(float), ExpressionType.NotEqual), (left, right) => (float)left() != (float)right()},
          {new Tuple<Type, ExpressionType>(typeof(double), ExpressionType.NotEqual), (left, right) => (double)left() != (double)right()},
          {new Tuple<Type, ExpressionType>(typeof(decimal), ExpressionType.NotEqual), (left, right) => (decimal)left() != (decimal)right()},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.LessThan), (left, right) => (sbyte)left() < (sbyte)right()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.LessThan), (left, right) => (byte)left() < (byte)right()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.LessThan), (left, right) => (short)left() < (short)right()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.LessThan), (left, right) => (ushort)left() < (ushort)right()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.LessThan), (left, right) => (int)left() < (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.LessThan), (left, right) => (uint)left() < (uint)right()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.LessThan), (left, right) => (long)left() < (long)right()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.LessThan), (left, right) => (char)left() < (char)right()},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.LessThan), (left, right) => (ulong)left() < (ulong)right()},
          {new Tuple<Type, ExpressionType>(typeof(float), ExpressionType.LessThan), (left, right) => (float)left() < (float)right()},
          {new Tuple<Type, ExpressionType>(typeof(double), ExpressionType.LessThan), (left, right) => (double)left() < (double)right()},
          {new Tuple<Type, ExpressionType>(typeof(decimal), ExpressionType.LessThan), (left, right) => (decimal)left() < (decimal)right()},
          
          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.LessThanOrEqual), (left, right) => (sbyte)left() <= (sbyte)right()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.LessThanOrEqual), (left, right) => (byte)left() <= (byte)right()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.LessThanOrEqual), (left, right) => (short)left() <= (short)right()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.LessThanOrEqual), (left, right) => (ushort)left() <= (ushort)right()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.LessThanOrEqual), (left, right) => (int)left() <= (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.LessThanOrEqual), (left, right) => (uint)left() <= (uint)right()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.LessThanOrEqual), (left, right) => (long)left() <= (long)right()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.LessThanOrEqual), (left, right) => (char)left() <= (char)right()},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.LessThanOrEqual), (left, right) => (ulong)left() <= (ulong)right()},
          {new Tuple<Type, ExpressionType>(typeof(float), ExpressionType.LessThanOrEqual), (left, right) => (float)left() <= (float)right()},
          {new Tuple<Type, ExpressionType>(typeof(double), ExpressionType.LessThanOrEqual), (left, right) => (double)left() <= (double)right()},
          {new Tuple<Type, ExpressionType>(typeof(decimal), ExpressionType.LessThanOrEqual), (left, right) => (decimal)left() <= (decimal)right()},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.GreaterThan), (left, right) => (sbyte)left() > (sbyte)right()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.GreaterThan), (left, right) => (byte)left() > (byte)right()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.GreaterThan), (left, right) => (short)left() > (short)right()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.GreaterThan), (left, right) => (ushort)left() > (ushort)right()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.GreaterThan), (left, right) => (int)left() > (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.GreaterThan), (left, right) => (uint)left() > (uint)right()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.GreaterThan), (left, right) => (long)left() > (long)right()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.GreaterThan), (left, right) => (char)left() > (char)right()},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.GreaterThan), (left, right) => (ulong)left() > (ulong)right()},
          {new Tuple<Type, ExpressionType>(typeof(float), ExpressionType.GreaterThan), (left, right) => (float)left() > (float)right()},
          {new Tuple<Type, ExpressionType>(typeof(double), ExpressionType.GreaterThan), (left, right) => (double)left() > (double)right()},
          {new Tuple<Type, ExpressionType>(typeof(decimal), ExpressionType.GreaterThan), (left, right) => (decimal)left() > (decimal)right()},
          
          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.GreaterThanOrEqual), (left, right) => (sbyte)left() >= (sbyte)right()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.GreaterThanOrEqual), (left, right) => (byte)left() >= (byte)right()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.GreaterThanOrEqual), (left, right) => (short)left() >= (short)right()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.GreaterThanOrEqual), (left, right) => (ushort)left() >= (ushort)right()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.GreaterThanOrEqual), (left, right) => (int)left() >= (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.GreaterThanOrEqual), (left, right) => (uint)left() >= (uint)right()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.GreaterThanOrEqual), (left, right) => (long)left() >= (long)right()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.GreaterThanOrEqual), (left, right) => (char)left() >= (char)right()},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.GreaterThanOrEqual), (left, right) => (ulong)left() >= (ulong)right()},
          {new Tuple<Type, ExpressionType>(typeof(float), ExpressionType.GreaterThanOrEqual), (left, right) => (float)left() >= (float)right()},
          {new Tuple<Type, ExpressionType>(typeof(double), ExpressionType.GreaterThanOrEqual), (left, right) => (double)left() >= (double)right()},
          {new Tuple<Type, ExpressionType>(typeof(decimal), ExpressionType.GreaterThanOrEqual), (left, right) => (decimal)left() >= (decimal)right()},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.RightShift), (left, right) => (sbyte)left() >> (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.RightShift), (left, right) => (byte)left() >> (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.RightShift), (left, right) => (short)left() >> (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.RightShift), (left, right) => (ushort)left() >> (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.RightShift), (left, right) => (int)left() >> (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.RightShift), (left, right) => (uint)left() >> (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.RightShift), (left, right) => (long)left() >> (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.RightShift), (left, right) => (char)left() >> (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.RightShift), (left, right) => (ulong)left() >> (int)right()},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.LeftShift), (left, right) => (sbyte)left() << (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.LeftShift), (left, right) => (byte)left() << (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.LeftShift), (left, right) => (short)left() << (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.LeftShift), (left, right) => (ushort)left() << (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.LeftShift), (left, right) => (int)left() << (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.LeftShift), (left, right) => (uint)left() << (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.LeftShift), (left, right) => (long)left() << (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.LeftShift), (left, right) => (char)left() << (int)right()},
          {new Tuple<Type, ExpressionType>(typeof(ulong), ExpressionType.LeftShift), (left, right) => (ulong)left() << (int)right()},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.Negate), (val, _) => -(sbyte)val()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.Negate), (val, _) => -(byte)val()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.Negate), (val, _) => -(short)val()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.Negate), (val, _) => -(ushort)val()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.Negate), (val, _) => -(int)val()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.Negate), (val, _) => -(uint)val()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.Negate), (val, _) => -(long)val()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.Negate), (val, _) => -(char)val()},
          {new Tuple<Type, ExpressionType>(typeof(float), ExpressionType.Negate), (val, _) => -(float)val()},
          {new Tuple<Type, ExpressionType>(typeof(double), ExpressionType.Negate), (val, _) => -(double)val()},
          {new Tuple<Type, ExpressionType>(typeof(decimal), ExpressionType.Negate), (val, _) => -(decimal)val()},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.NegateChecked), (val, _) => checked(-(sbyte)val())},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.NegateChecked), (val, _) => checked(-(byte)val())},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.NegateChecked), (val, _) => checked(-(short)val())},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.NegateChecked), (val, _) => checked(-(ushort)val())},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.NegateChecked), (val, _) => checked(-(int)val())},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.NegateChecked), (val, _) => checked(-(uint)val())},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.NegateChecked), (val, _) => checked(-(long)val())},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.NegateChecked), (val, _) => checked(-(char)val())},
          {new Tuple<Type, ExpressionType>(typeof(float), ExpressionType.NegateChecked), (val, _) => checked(-(float)val())},
          {new Tuple<Type, ExpressionType>(typeof(double), ExpressionType.NegateChecked), (val, _) => checked(-(double)val())},
          {new Tuple<Type, ExpressionType>(typeof(decimal), ExpressionType.NegateChecked), (val, _) => checked(-(decimal)val())},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.UnaryPlus), (val, _) => +(sbyte)val()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.UnaryPlus), (val, _) => +(byte)val()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.UnaryPlus), (val, _) => +(short)val()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.UnaryPlus), (val, _) => +(ushort)val()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.UnaryPlus), (val, _) => +(int)val()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.UnaryPlus), (val, _) => +(uint)val()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.UnaryPlus), (val, _) => +(long)val()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.UnaryPlus), (val, _) => +(char)val()},
          {new Tuple<Type, ExpressionType>(typeof(float), ExpressionType.UnaryPlus), (val, _) => +(float)val()},
          {new Tuple<Type, ExpressionType>(typeof(double), ExpressionType.UnaryPlus), (val, _) => +(double)val()},
          {new Tuple<Type, ExpressionType>(typeof(decimal), ExpressionType.UnaryPlus), (val, _) => +(decimal)val()},

          {new Tuple<Type, ExpressionType>(typeof(sbyte), ExpressionType.Not), (val, _) => ~(sbyte)val()},
          {new Tuple<Type, ExpressionType>(typeof(byte), ExpressionType.Not), (val, _) => ~(byte)val()},
          {new Tuple<Type, ExpressionType>(typeof(short), ExpressionType.Not), (val, _) => ~(short)val()},
          {new Tuple<Type, ExpressionType>(typeof(ushort), ExpressionType.Not), (val, _) => ~(ushort)val()},
          {new Tuple<Type, ExpressionType>(typeof(int), ExpressionType.Not), (val, _) => ~(int)val()},
          {new Tuple<Type, ExpressionType>(typeof(uint), ExpressionType.Not), (val, _) => ~(uint)val()},
          {new Tuple<Type, ExpressionType>(typeof(long), ExpressionType.Not), (val, _) => ~(long)val()},
          {new Tuple<Type, ExpressionType>(typeof(char), ExpressionType.Not), (val, _) => ~(char)val()},
          {new Tuple<Type, ExpressionType>(typeof(bool), ExpressionType.Not), (val, _) => !(bool)val()},
        };
      }

      private Scope values = new Scope();

      private Func<T> Memoize<T>(Func<T> func) {
        bool hasRun = false;
        T value = default(T);
        return () => {
          if (!hasRun) {
            value = func();
            hasRun = true;
          }
          return value;
        };
      }

      protected override Expression VisitBinary(BinaryExpression expr) {
        var left = Memoize<object>(() => ((ConstantExpression)Visit(expr.Left)).Value);
        var right = Memoize<object>(() => ((ConstantExpression)Visit(expr.Right)).Value);
        Func<Func<object>, Func<object>, object> converter;

        // For enums, rewrite as conversions to their underlying types and then a conversion back.
        var unliftedLeft = Unlift(expr.Left.Type);
        var unliftedRight = Unlift(expr.Right.Type);
        if (unliftedLeft.IsEnum && unliftedLeft.Equals(unliftedRight)) {
          var underlyingType = Enum.GetUnderlyingType(unliftedLeft);
          var nullableUnderlying = typeof(Nullable<>).MakeGenericType(underlyingType);
          return Visit(Expression.Convert(
              expr.Update(
                  Expression.Convert(expr.Left, nullableUnderlying),
                  expr.Conversion,
                  Expression.Convert(expr.Right, nullableUnderlying)),
              expr.Type));
        }
        object result = null;
        if (!unliftedLeft.IsPrimitive) {
          if (expr.NodeType == ExpressionType.AndAlso || expr.NodeType == ExpressionType.OrElse) {
            // Check truth first
            var truthMethod = unliftedLeft.GetMethod(expr.NodeType == ExpressionType.AndAlso ? "op_False" : "op_True", new[] {unliftedLeft});
            if (truthMethod != null && (bool)truthMethod.Invoke(null, new object[]{left()})) {
              return Expression.Constant(left(), expr.Type);
            }
            if (expr.IsLiftedToNull && right() == null) {
              return Expression.Constant(null, expr.Type);
            }
            if (expr.Method != null) {
              return Expression.Constant(expr.Method.Invoke(null, new object[] {
                left(),
                right()
              }), expr.Type);
            }
          }
        }
        if (expr.IsLiftedToNull &&
          (expr.Left.Type.Equals(typeof(bool?)) || expr.Left.Type.Equals(typeof(bool))) &&
          (expr.Right.Type.Equals(typeof(bool?)) || expr.Right.Type.Equals(typeof(bool))) &&
          expr.Type.Equals(typeof(bool?)) &&
          (expr.NodeType == ExpressionType.And || expr.NodeType == ExpressionType.Or)) {
          Func<bool?, bool?, bool?> evaluator = null;
          switch (expr.NodeType) {
          case ExpressionType.And:
            evaluator = (l, r) => l & r;
            break;
          case ExpressionType.Or:
            evaluator = (l, r) => l | r;
            break;
          }
          return Expression.Constant(evaluator.DynamicInvoke(left(), right()), expr.Type);
        }
        if (expr.IsLiftedToNull) {
          if ((expr.Left.Type.Equals(typeof(bool?)) || expr.Left.Type.Equals(typeof(bool))) &&
            (expr.Right.Type.Equals(typeof(bool?)) || expr.Right.Type.Equals(typeof(bool)))) {
            if (expr.NodeType == ExpressionType.AndAlso && false.Equals(left())) {
              return Expression.Constant(false, expr.Type);
            }
            if (expr.NodeType == ExpressionType.OrElse && true.Equals(left())) {
              return Expression.Constant(true, expr.Type);
            }
          }
          if (left() == null || right() == null) {
            return Expression.Constant(null, expr.Type);
          }
        }
        if (expr.IsLifted) {
          switch (expr.NodeType) {
          case ExpressionType.LessThan:
          case ExpressionType.LessThanOrEqual:
          case ExpressionType.GreaterThan:
          case ExpressionType.GreaterThanOrEqual:
            if (left() == null || right() == null) {
              return Expression.Constant(false, expr.Type);
            }
            break;
          case ExpressionType.Equal:
            if (left() == null || right() == null) {
              return Expression.Constant(left() == right(), expr.Type);
            }
            break;
          case ExpressionType.NotEqual:
            if (left() == null || right() == null) {
              return Expression.Constant(left() != right(), expr.Type);
            }
            break;
          }
        }
        if (expr.Method != null) {
          result = expr.Method.Invoke(null, new[]{left(), right()});
        } else if (builtinConversions.TryGetValue(
            new Tuple<Type, ExpressionType>(unliftedLeft, expr.NodeType),
            out converter)) {
          result = Convert.ChangeType(converter(left, right), Unlift(expr.Type));
        } else {
          switch (expr.NodeType) {
          case ExpressionType.Coalesce:
            if (left() != null) {
              result = left();
              if (expr.Conversion != null) {
                result = Evaluate(expr.Conversion, values, result);
              }
            } else {
              result = right();
            }
            break;
          case ExpressionType.ArrayIndex:
            if (expr.Right.Type.Equals(typeof(int))) {
              result = ((Array)left()).GetValue((int)right());
            } else {
              result = ((Array)left()).GetValue((long)right());
            }
            break;
          }
        }
        return Expression.Constant(result, expr.Type);
      }

      protected override Expression VisitMember(MemberExpression expr) {
        object root;
        if (expr.Expression == null) {
          root = null;
        } else {
          root = ((ConstantExpression)Visit(expr.Expression)).Value;
          if (IsNullable(expr.Expression.Type)) {
            return Expression.Constant(PerformOnNullable(root, expr.Member, new Expression[0]), expr.Type);
          }
        }
        return Expression.Constant(expr.Member.Get(root));
      }

      private bool IsNullable(Type t) {
        return t.IsGenericType && t.GetGenericTypeDefinition().Equals(typeof(Nullable<>));
      }

      private Type Unlift(Type t) {
        if (IsNullable(t)) {
          return t.GetGenericArguments()[0];
        }
        return t;
      }

      protected override Expression VisitUnary(UnaryExpression expr) {
        if (expr.NodeType == ExpressionType.Quote) {
          return Expression.Constant(new ParameterReplacer(values).Visit(expr.Operand), expr.Type);
        }
        var val = Memoize<object>(() => ((ConstantExpression)Visit(expr.Operand)).Value);
        if (expr.IsLiftedToNull && val() == null) {
          return Expression.Constant(null, expr.Type);
        }
        if (expr.Method != null) {
          var parameterType = expr.Method.GetParameters()[0].ParameterType;
          if (val() == null && parameterType.IsValueType && !IsNullable(parameterType)) {
            throw new InvalidOperationException("Cannot pass null into a conversion expecting a value type.");
          }
          return Expression.Constant(expr.Method.Invoke(null, new[]{val()}), expr.Type);
        }
        Type realSourceType;
        Type realTargetType;
        if (expr.IsLifted) {
          realSourceType = Unlift(expr.Operand.Type);
          realTargetType = Unlift(expr.Type);
        } else {
          realSourceType = expr.Operand.Type;
          realTargetType = expr.Type;
        }
        switch (expr.NodeType) {
        case ExpressionType.TypeAs:
          return Expression.Constant(expr.Type.IsInstanceOfType(val()) ? val() : null, expr.Type);
        case ExpressionType.Convert:
          return Expression.Constant(Convert.ChangeType(val(), realTargetType), expr.Type);
        case ExpressionType.ConvertChecked:
          var convertMethod = typeof(Convert).GetMethod("To" + realTargetType.Name, new[] {val().GetType()});
          return Expression.Constant(convertMethod.Invoke(null, new object[]{val()}), expr.Type);
        case ExpressionType.ArrayLength:
          return Expression.Constant(((Array)val()).Length, expr.Type);
        }
        Func<Func<object>, Func<object>, object> op;
        if (builtinConversions.TryGetValue(new Tuple<Type, ExpressionType>(realSourceType, expr.NodeType), out op)) {
          return Expression.Constant(op(val, null), expr.Type);
        }
        throw new NotSupportedException("Bad unary operation: " + expr);
      }

      private object InvokeMethod(Func<object[], object> toInvoke, IEnumerable<Expression> arguments) {
        // Precalculate argument expressions, holding onto any fields in case of reference parameters.
        var exprArray = arguments.Select(a => {
          var index = a as BinaryExpression;
          if (index != null && index.NodeType == ExpressionType.ArrayIndex) {
            return index.Update(Visit(index.Left), null, Visit(index.Right));
          }
          var fieldAccess = a as MemberExpression;
          if (fieldAccess != null && fieldAccess.Member is FieldInfo) {
            return fieldAccess.Update(Visit(fieldAccess.Expression));
          }
          var call = a as MethodCallExpression;
          if (call != null && typeof(Array).IsAssignableFrom(call.Object.Type) && call.Method.Name.Equals("Get")) {
            return call.Update(Visit(call.Object), call.Arguments.Select(a2 => Visit(a2)));
          }
          return Visit(a);
        }).ToArray();
        var originalArray = exprArray.Select(arg => ((ConstantExpression)Visit(arg)).Value).ToArray();
        var argCopy = originalArray.ToArray();
        var result = toInvoke(argCopy);
        foreach (var element in Enumerable.Range(0, exprArray.Length)
                 .Select(i=>new {TrueOriginal = arguments.ElementAt(i), Expression = exprArray[i], Original = originalArray[i], Modified = argCopy[i]})) {
          if (!object.ReferenceEquals(element.Original, element.Modified)) {
            var index = element.Expression as BinaryExpression;
            if (index != null) {
              var arr = ((ConstantExpression)index.Left).Value as Array;
              var indices = ((ConstantExpression)index.Right).Value;
              if (indices is int) {
                arr.SetValue(element.Modified, (int)indices);
              } else if (indices is long) {
                arr.SetValue(element.Modified, (long)indices);
              } else if (indices is int[]) {
                arr.SetValue(element.Modified, (int[])indices);
              } else {
                arr.SetValue(element.Modified, (long[])indices);
              }
              continue;
            }
            var multiIndex = element.Expression as MethodCallExpression;
            if (multiIndex != null) {
              var arr = ((ConstantExpression)multiIndex.Object).Value as Array;
              if (multiIndex.Arguments[0].Type.Equals(typeof(int))) {
                arr.SetValue(element.Modified, multiIndex.Arguments.Select(a => (int)((ConstantExpression)a).Value).ToArray());
              } else {
                arr.SetValue(element.Modified, multiIndex.Arguments.Select(a => (long)((ConstantExpression)a).Value).ToArray());
              }
            }
            var fieldAccess = element.Expression as MemberExpression;
            if (fieldAccess != null) {
              var fieldRoot = ((ConstantExpression)fieldAccess.Expression).Value;
              fieldAccess.Member.Set(fieldRoot, element.Modified);
              continue;
            }
          }
        }
        return result;
      }

      private object PerformOnNullable(object root, MemberInfo member, IEnumerable<Expression> arguments) {
        var args = arguments.Select(a => ((ConstantExpression)Visit(a)).Value).ToArray();
        if (member.Name.Equals("HasValue")) {
          return root != null;
        }
        if (member.Name.Equals("Value")) {
          if (root == null) {
            throw new InvalidOperationException("Nullable object must have a value.");
          }
          return root;
        }
        if (member.Name.Equals("Equals")) {
          return object.Equals(root, args[0]);
        }
        if (member.Name.Equals("GetHashCode")) {
          if (root == null) {
            return 0;
          }
          return root.GetHashCode();
        }
        if (member.Name.Equals("GetValueOrDefault")) {
          if (root == null) {
            return args.FirstOrDefault();
          }
          return root;
        }
        if (member.Name.Equals("ToString")) {
          if (root == null) {
            return string.Empty;
          }
          return root.ToString();
        }
        throw new NotSupportedException("Cannot call on Nullable");
      }

      protected override Expression VisitMethodCall(MethodCallExpression expr) {
        object root;
        if (expr.Method.IsStatic) {
          root = null;
        } else {
          root = ((ConstantExpression)Visit(expr.Object)).Value;
          if (IsNullable(expr.Object.Type)) {
            return Expression.Constant(PerformOnNullable(root, expr.Method, expr.Arguments), expr.Type);
          }
        }
        return Expression.Constant(
            InvokeMethod(args => expr.Method.Invoke(root, args), expr.Arguments),
            expr.Type.Equals(typeof(void)) ? typeof(object) : expr.Type);
      }

      protected override Expression VisitConditional(ConditionalExpression expr) {
        return ((bool)((ConstantExpression)Visit(expr.Test)).Value) ? Visit(expr.IfTrue) : Visit(expr.IfFalse);
      }

      protected override Expression VisitListInit(ListInitExpression expr) {
        var newValue = ((ConstantExpression)Visit(expr.NewExpression)).Value;
        ApplyElementInitializers(newValue, expr.Initializers.Select(i => VisitElementInit(i)));
        return Expression.Constant(newValue, expr.Type);
      }

      protected override Expression VisitMemberInit(MemberInitExpression expr) {
        var newValue = ((ConstantExpression)Visit(expr.NewExpression)).Value;
        ApplyMemberBindings(newValue, expr.Bindings.Select(b => VisitMemberBinding(b)));
        return Expression.Constant(newValue, expr.Type);
      }

      protected override Expression VisitNew(NewExpression expr) {
        if (expr.Constructor == null && expr.Type.IsValueType) {
          if (expr.Type.Equals(typeof(TypedReference))) {
            return Expression.Constant(new TypedReference(), expr.Type);
          }
          if (expr.Type.Equals(typeof(ArgIterator))) {
            return Expression.Constant(new ArgIterator(), expr.Type);
          }
          if (expr.Type.Equals(typeof(RuntimeArgumentHandle))) {
            return Expression.Constant(new RuntimeArgumentHandle(), expr.Type);
          }
          return Expression.Constant(Activator.CreateInstance(expr.Type), expr.Type);
        }
        return Expression.Constant(
            expr.Constructor.Invoke(expr.Arguments.Select(a => ((ConstantExpression)Visit(a)).Value).ToArray()),
            expr.Type);
      }

      protected override Expression VisitNewArray(NewArrayExpression expr) {
        var arrayType = expr.Type.GetElementType();
        var arguments = expr.Expressions.Select(e => ((ConstantExpression)Visit(e)).Value).ToArray();
        switch (expr.NodeType) {
        case ExpressionType.NewArrayBounds:
          return Expression.Constant(Array.CreateInstance(arrayType, arguments.Select(a => Convert.ToInt64(a)).ToArray()), expr.Type);
        case ExpressionType.NewArrayInit:
          var arr = Array.CreateInstance(arrayType, arguments.Length);
          Array.Copy(arguments, arr, arr.Length);
          return Expression.Constant(arr, expr.Type);
        default:
          throw new NotSupportedException("Bad new array expression: " + expr);
        }
      }

      protected override Expression VisitTypeBinary(TypeBinaryExpression expr) {
        return Expression.Constant(
            expr.TypeOperand.IsInstanceOfType(((ConstantExpression)Visit(expr.Expression)).Value),
            expr.Type);
      }

      protected override Expression VisitParameter(ParameterExpression expr) {
        return Expression.Constant(values[expr], expr.Type);
      }

      protected override Expression VisitLambda<T>(Expression<T> expr) {
        var delType = typeof(T);
        var method = delType.GetMethod("Invoke");
        var genericList = method.GetParameters().Select(p => p.ParameterType).ToList();
        // Pad the list
        while (genericList.Count < 16) {
          genericList.Add(typeof(GeneralFunc));
        }

        MethodInfo maker;
        if (!method.ReturnType.Equals(typeof(void))) {
          genericList.Add(method.ReturnType);
          Expression<Action> makeFuncExpr = () => MakeFunc<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T>(null);
          maker = ((MethodCallExpression)makeFuncExpr.Body).Method.GetGenericMethodDefinition();
          maker = maker.MakeGenericMethod(genericList.ToArray());
        } else {
          Expression<Action> makeActionExpr = () => MakeAction<T, T, T, T, T, T, T, T, T, T, T, T, T, T, T, T>(null);
          maker = ((MethodCallExpression)makeActionExpr.Body).Method.GetGenericMethodDefinition();
          maker = maker.MakeGenericMethod(genericList.ToArray());
        }

        var del = (Delegate)maker.Invoke(null, new object[] { (GeneralFunc)(args => Evaluate(expr, values, args)) });
        if (!(del is T)) {
          del = Delegate.CreateDelegate(delType, del.Target, del.Method);
        }
        return Expression.Constant(del, expr.Type);
      }

      protected override Expression VisitInvocation(InvocationExpression expr) {
        var toInvoke = (Delegate)((ConstantExpression)Visit(expr.Expression)).Value;
        var result = InvokeMethod(args => toInvoke.DynamicInvoke(args), expr.Arguments);
        return Expression.Constant(result, expr.Type.Equals(typeof(void)) ? typeof(object) : expr.Type);
      }

      private delegate object GeneralFunc(params object[] args);

      private static Delegate MakeFunc<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult>(GeneralFunc toCall) {
        if (!typeof(T16).Equals(typeof(GeneralFunc))) {
          Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult> result = 
            (o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12, o13, o14, o15, o16) =>
              (TResult)toCall(o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12, o13, o14, o15, o16);
          return result;
        }
        if (!typeof(T15).Equals(typeof(GeneralFunc))) {
          Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult> result = 
            (o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12, o13, o14, o15) =>
              (TResult)toCall(o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12, o13, o14, o15);
          return result;
        }
        if (!typeof(T14).Equals(typeof(GeneralFunc))) {
          Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult> result = 
            (o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12, o13, o14) =>
              (TResult)toCall(o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12, o13, o14);
          return result;
        }
        if (!typeof(T13).Equals(typeof(GeneralFunc))) {
          Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult> result = 
            (o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12, o13) =>
              (TResult)toCall(o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12, o13);
          return result;
        }
        if (!typeof(T12).Equals(typeof(GeneralFunc))) {
          Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult> result = 
            (o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12) =>
              (TResult)toCall(o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12);
          return result;
        }
        if (!typeof(T11).Equals(typeof(GeneralFunc))) {
          Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult> result = 
            (o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11) =>
              (TResult)toCall(o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11);
          return result;
        }
        if (!typeof(T10).Equals(typeof(GeneralFunc))) {
          Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> result = 
            (o1, o2, o3, o4, o5, o6, o7, o8, o9, o10) =>
              (TResult)toCall(o1, o2, o3, o4, o5, o6, o7, o8, o9, o10);
          return result;
        }
        if (!typeof(T9).Equals(typeof(GeneralFunc))) {
          Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult> result = 
            (o1, o2, o3, o4, o5, o6, o7, o8, o9) =>
              (TResult)toCall(o1, o2, o3, o4, o5, o6, o7, o8, o9);
          return result;
        }
        if (!typeof(T8).Equals(typeof(GeneralFunc))) {
          Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> result = 
            (o1, o2, o3, o4, o5, o6, o7, o8) =>
              (TResult)toCall(o1, o2, o3, o4, o5, o6, o7, o8);
          return result;
        }
        if (!typeof(T7).Equals(typeof(GeneralFunc))) {
          Func<T1, T2, T3, T4, T5, T6, T7, TResult> result = 
            (o1, o2, o3, o4, o5, o6, o7) =>
              (TResult)toCall(o1, o2, o3, o4, o5, o6, o7);
          return result;
        }
        if (!typeof(T6).Equals(typeof(GeneralFunc))) {
          Func<T1, T2, T3, T4, T5, T6, TResult> result = 
            (o1, o2, o3, o4, o5, o6) =>
              (TResult)toCall(o1, o2, o3, o4, o5, o6);
          return result;
        }
        if (!typeof(T5).Equals(typeof(GeneralFunc))) {
          Func<T1, T2, T3, T4, T5, TResult> result = 
            (o1, o2, o3, o4, o5) =>
              (TResult)toCall(o1, o2, o3, o4, o5);
          return result;
        }
        if (!typeof(T4).Equals(typeof(GeneralFunc))) {
          Func<T1, T2, T3, T4, TResult> result = 
            (o1, o2, o3, o4) =>
              (TResult)toCall(o1, o2, o3, o4);
          return result;
        }
        if (!typeof(T3).Equals(typeof(GeneralFunc))) {
          Func<T1, T2, T3, TResult> result = 
            (o1, o2, o3) =>
              (TResult)toCall(o1, o2, o3);
          return result;
        }
        if (!typeof(T2).Equals(typeof(GeneralFunc))) {
          Func<T1, T2, TResult> result = 
            (o1, o2) =>
              (TResult)toCall(o1, o2);
          return result;
        }
        if (!typeof(T1).Equals(typeof(GeneralFunc))) {
          Func<T1, TResult> result = 
            (o1) =>
              (TResult)toCall(o1);
          return result;
        }
        return (Func<TResult>)(() => (TResult)toCall());
      }

      private static Delegate MakeAction<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(GeneralFunc toCall) {
        if (!typeof(T16).Equals(typeof(GeneralFunc))) {
          Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> result = 
            (o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12, o13, o14, o15, o16) =>
              toCall(o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12, o13, o14, o15, o16);
          return result;
        }
        if (!typeof(T15).Equals(typeof(GeneralFunc))) {
          Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> result = 
            (o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12, o13, o14, o15) =>
              toCall(o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12, o13, o14, o15);
          return result;
        }
        if (!typeof(T14).Equals(typeof(GeneralFunc))) {
          Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> result = 
            (o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12, o13, o14) =>
              toCall(o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12, o13, o14);
          return result;
        }
        if (!typeof(T13).Equals(typeof(GeneralFunc))) {
          Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> result = 
            (o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12, o13) =>
              toCall(o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12, o13);
          return result;
        }
        if (!typeof(T12).Equals(typeof(GeneralFunc))) {
          Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> result = 
            (o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12) =>
              toCall(o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12);
          return result;
        }
        if (!typeof(T11).Equals(typeof(GeneralFunc))) {
          Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> result = 
            (o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11) =>
              toCall(o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11);
          return result;
        }
        if (!typeof(T10).Equals(typeof(GeneralFunc))) {
          Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> result = 
            (o1, o2, o3, o4, o5, o6, o7, o8, o9, o10) =>
              toCall(o1, o2, o3, o4, o5, o6, o7, o8, o9, o10);
          return result;
        }
        if (!typeof(T9).Equals(typeof(GeneralFunc))) {
          Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> result = 
            (o1, o2, o3, o4, o5, o6, o7, o8, o9) =>
              toCall(o1, o2, o3, o4, o5, o6, o7, o8, o9);
          return result;
        }
        if (!typeof(T8).Equals(typeof(GeneralFunc))) {
          Action<T1, T2, T3, T4, T5, T6, T7, T8> result = 
            (o1, o2, o3, o4, o5, o6, o7, o8) =>
              toCall(o1, o2, o3, o4, o5, o6, o7, o8);
          return result;
        }
        if (!typeof(T7).Equals(typeof(GeneralFunc))) {
          Action<T1, T2, T3, T4, T5, T6, T7> result = 
            (o1, o2, o3, o4, o5, o6, o7) =>
              toCall(o1, o2, o3, o4, o5, o6, o7);
          return result;
        }
        if (!typeof(T6).Equals(typeof(GeneralFunc))) {
          Action<T1, T2, T3, T4, T5, T6> result = 
            (o1, o2, o3, o4, o5, o6) =>
              toCall(o1, o2, o3, o4, o5, o6);
          return result;
        }
        if (!typeof(T5).Equals(typeof(GeneralFunc))) {
          Action<T1, T2, T3, T4, T5> result = 
            (o1, o2, o3, o4, o5) =>
              toCall(o1, o2, o3, o4, o5);
          return result;
        }
        if (!typeof(T4).Equals(typeof(GeneralFunc))) {
          Action<T1, T2, T3, T4> result = 
            (o1, o2, o3, o4) =>
              toCall(o1, o2, o3, o4);
          return result;
        }
        if (!typeof(T3).Equals(typeof(GeneralFunc))) {
          Action<T1, T2, T3> result = 
            (o1, o2, o3) =>
              toCall(o1, o2, o3);
          return result;
        }
        if (!typeof(T2).Equals(typeof(GeneralFunc))) {
          Action<T1, T2> result = 
            (o1, o2) =>
              toCall(o1, o2);
          return result;
        }
        if (!typeof(T1).Equals(typeof(GeneralFunc))) {
          Action<T1> result = 
            (o1) =>
              toCall(o1);
          return result;
        }
        return (Action)(() => toCall());
      }

      private void ApplyMemberBindings(object root, IEnumerable<MemberBinding> bindings) {
        foreach (var init in bindings) {
          switch (init.BindingType) {
          case MemberBindingType.Assignment:
            var assgn = (MemberAssignment)init;
            assgn.Member.Set(root, ((ConstantExpression)assgn.Expression).Value);
            break;
          case MemberBindingType.ListBinding:
            var list = (MemberListBinding)init;
            var listRoot = list.Member.Get(root);
            ApplyElementInitializers(listRoot, list.Initializers);
            break;
          case MemberBindingType.MemberBinding:
            var member = (MemberMemberBinding)init;
            var memberRoot = member.Member.Get(root);
            ApplyMemberBindings(memberRoot, member.Bindings);
            break;
          default:
            throw new NotSupportedException("Bad binding: " + init);
          }
        }
      }

      private void ApplyElementInitializers(object root, IEnumerable<ElementInit> initializers) {
        foreach (var init in initializers) {
          init.AddMethod.Invoke(root, init.Arguments.Select(a => ((ConstantExpression)a).Value).ToArray());
        }
      }

      internal static object Evaluate(LambdaExpression expr, Scope scope, params object[] args) {
        var visitor = new EvaluatingVisitor();
        visitor.values = new Scope(scope);
        foreach (var pair in expr.Parameters.Zip(args, (param, val) => new {param, val})) {
          visitor.values.Register(pair.param, pair.val);
        }
        var result = visitor.Visit(expr.Body);
        return ((ConstantExpression)result).Value;
      }
    }

    private static object Get(this MemberInfo info, object root) {
      var field = info as FieldInfo;
      if (field != null) {
        return field.GetValue(root);
      }
      var property = info as PropertyInfo;
      if (property != null) {
        return property.GetValue(root, null);
      }
      throw new NotSupportedException("Bad MemberInfo type.");
    }

    private static void Set(this MemberInfo info, object root, object value) {
      var field = info as FieldInfo;
      if (field != null) {
        field.SetValue(root, value);
        return;
      }
      var property = info as PropertyInfo;
      if (property != null) {
        property.SetValue(root, value, null);
        return;
      }
      throw new NotSupportedException("Bad MemberInfo type.");
    }
    
#if IOS
    [MonoTouch.Foundation.Preserve]
    private static void ExcludeFromLinking() {
      new List<Action> {
        () => new StrongBox<object>(null)
      };
    }
#endif

    public static T Interpret<T>(this Expression<T> expr) {
      return (T)((ConstantExpression)new EvaluatingVisitor().Visit(expr)).Value;
    }

    internal static BinaryExpression Update(this BinaryExpression expr, Expression left, LambdaExpression conversion, Expression right) {
      return Expression.MakeBinary(expr.NodeType, left, right, expr.IsLiftedToNull, expr.Method, conversion);
    }

    internal static ConditionalExpression Update(this ConditionalExpression expr, Expression test, Expression isTrue, Expression isFalse) {
      return Expression.Condition(test, isTrue, isFalse);
    }

    internal static ElementInit Update(this ElementInit init, IEnumerable<Expression> arguments) {
      return Expression.ElementInit(init.AddMethod, arguments);
    }

    internal static LambdaExpression Update(this LambdaExpression expr, Expression body, IEnumerable<ParameterExpression> parameters) {
      return Expression.Lambda(expr.Type, body, parameters);
    }

    internal static ListInitExpression Update(this ListInitExpression expr, NewExpression newExpr, IEnumerable<ElementInit> initializers) {
      return Expression.ListInit(newExpr, initializers);
    }

    internal static MemberExpression Update(this MemberExpression expr, Expression obj) {
      return Expression.MakeMemberAccess(obj, expr.Member);
    }

    internal static MemberAssignment Update(this MemberAssignment assgn, Expression expr) {
      return Expression.Bind(assgn.Member, expr);
    }

    internal static InvocationExpression Update(this InvocationExpression expr, Expression root, IEnumerable<Expression> args) {
      return Expression.Invoke(root, args);
    }

    internal static MemberInitExpression Update(this MemberInitExpression expr, NewExpression newExpr, IEnumerable<MemberBinding> bindings) {
      return Expression.MemberInit(newExpr, bindings);
    }

    internal static MemberListBinding Update(this MemberListBinding binding, IEnumerable<ElementInit> initializers) {
      return Expression.ListBind(binding.Member, initializers);
    }

    internal static MemberMemberBinding Update(this MemberMemberBinding binding, IEnumerable<MemberBinding> bindings) {
      return Expression.MemberBind(binding.Member, bindings);
    }

    internal static MethodCallExpression Update(this MethodCallExpression expr, Expression root, IEnumerable<Expression> args) {
      return Expression.Call(root, expr.Method, args);
    }

    internal static NewExpression Update(this NewExpression expr, IEnumerable<Expression> args) {
      return Expression.New(expr.Constructor, args, expr.Members);
    }

    internal static NewArrayExpression Update(this NewArrayExpression expr, IEnumerable<Expression> args) {
      return Expression.NewArrayInit(expr.Type, args);
    }

    internal static TypeBinaryExpression Update(this TypeBinaryExpression expr, Expression body) {
      return Expression.TypeIs(body, expr.TypeOperand);
    }

    internal static UnaryExpression Update(this UnaryExpression expr, Expression body) {
      return Expression.MakeUnary(expr.NodeType, body, expr.Type, expr.Method);
    }
  }
}

