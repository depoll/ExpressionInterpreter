using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;

namespace ExpressionInterpreter {
  public abstract class ExpressionVisitor {
    public ExpressionVisitor() {
    }

    public virtual Expression Visit(Expression expr) {
      if (expr == null) {
        return null;
      }

      var bin = expr as BinaryExpression;
      if (bin != null) {
        return VisitBinary(bin);
      }

      var cond = expr as ConditionalExpression;
      if (cond != null) {
        return VisitConditional(cond);
      }

      var constant = expr as ConstantExpression;
      if (constant != null) {
        return VisitConstant(constant);
      }

      var lambda = expr as LambdaExpression;
      if (lambda != null) {
        var lambdaType = lambda.GetType().GetGenericArguments()[0];
        Expression<Func<object>> visitLambdaExpr = () => VisitLambda<object>(null);
        MethodInfo method = ((MethodCallExpression)visitLambdaExpr.Body).Method.GetGenericMethodDefinition().MakeGenericMethod(lambdaType);
        return (Expression)method.Invoke(this, new[] {lambda});
      }

      var listInit = expr as ListInitExpression;
      if (listInit != null) {
        return VisitListInit(listInit);
      }

      var member = expr as MemberExpression;
      if (member != null) {
        return VisitMember(member);
      }

      var memberInit = expr as MemberInitExpression;
      if (memberInit != null) {
        return VisitMemberInit(memberInit);
      }

      var methodCall = expr as MethodCallExpression;
      if (methodCall != null) {
        return VisitMethodCall(methodCall);
      }

      var newExpr = expr as NewExpression;
      if (newExpr != null) {
        return VisitNew(newExpr);
      }

      var newArrayExpr = expr as NewArrayExpression;
      if (newArrayExpr != null) {
        return VisitNewArray(newArrayExpr);
      }

      var param = expr as ParameterExpression;
      if (param != null) {
        return VisitParameter(param);
      }

      var typeBinary = expr as TypeBinaryExpression;
      if (typeBinary != null) {
        return VisitTypeBinary(typeBinary);
      }

      var unary = expr as UnaryExpression;
      if (unary != null) {
        return VisitUnary(unary);
      }

      var invocation = expr as InvocationExpression;
      if (invocation != null) {
        return VisitInvocation(invocation);
      }

      throw new NotSupportedException("Expressions of type " + expr.Type + " are not supported.");
    }

    protected virtual Expression VisitBinary(BinaryExpression expr) {
      return expr.Update(Visit(expr.Left), (LambdaExpression)Visit(expr.Conversion), Visit(expr.Right));
    }

    protected virtual Expression VisitConditional(ConditionalExpression expr) {
      return expr.Update(Visit(expr.Test), Visit(expr.IfTrue), Visit(expr.IfFalse));
    }

    protected virtual Expression VisitConstant(ConstantExpression expr) {
      return expr;
    }
  
    protected virtual ElementInit VisitElementInit(ElementInit init) {
      return init.Update(init.Arguments.Select(a => Visit(a)));
    }

    protected virtual Expression VisitLambda<T>(Expression<T> expr) {
      return expr.Update(Visit(expr.Body), expr.Parameters.Select(p => (ParameterExpression)VisitParameter(p)));
    }

    protected virtual Expression VisitListInit(ListInitExpression expr) {
      return expr.Update((NewExpression)Visit(expr.NewExpression), expr.Initializers.Select(i => VisitElementInit(i)));
    }

    protected virtual Expression VisitMember(MemberExpression expr) {
      return expr.Update(Visit(expr.Expression));
    }

    protected virtual MemberAssignment VisitMemberAssignment(MemberAssignment assgn) {
      return assgn.Update(Visit(assgn.Expression));
    }

    protected virtual MemberBinding VisitMemberBinding(MemberBinding binding) {
      switch (binding.BindingType) {
      case MemberBindingType.Assignment:
        return VisitMemberAssignment((MemberAssignment)binding);
      case MemberBindingType.ListBinding:
        return VisitMemberListBinding((MemberListBinding)binding);
      case MemberBindingType.MemberBinding:
        return VisitMemberMemberBinding((MemberMemberBinding)binding);
      default:
        throw new NotSupportedException("Bad member binding type: " + binding);
      }
    }

    protected virtual Expression VisitInvocation(InvocationExpression expr) {
      return expr.Update(Visit(expr.Expression), expr.Arguments.Select(a => Visit(a)));
    }

    protected virtual Expression VisitMemberInit(MemberInitExpression expr) {
      return expr.Update((NewExpression)Visit(expr.NewExpression), expr.Bindings.Select(b => VisitMemberBinding(b)));
    }

    protected virtual MemberListBinding VisitMemberListBinding(MemberListBinding binding) {
      return binding.Update(binding.Initializers.Select(i => VisitElementInit(i)));
    }

    protected virtual MemberMemberBinding VisitMemberMemberBinding(MemberMemberBinding binding) {
      return binding.Update(binding.Bindings.Select(b => VisitMemberBinding(b)));
    }

    protected virtual Expression VisitMethodCall(MethodCallExpression expr) {
      return expr.Update(Visit(expr.Object), expr.Arguments.Select(a => Visit(a)));
    }

    protected virtual Expression VisitNew(NewExpression expr) {
      return expr.Update(expr.Arguments.Select(a => Visit(a)));
    }

    protected virtual Expression VisitNewArray(NewArrayExpression expr) {
      return expr.Update(expr.Expressions.Select(a => Visit(a)));
    }

    protected virtual Expression VisitParameter(ParameterExpression expr) {
      return expr;
    }

    protected virtual Expression VisitTypeBinary(TypeBinaryExpression expr) {
      return expr.Update(Visit(expr.Expression));
    }

    protected virtual Expression VisitUnary(UnaryExpression expr) {
      return expr.Update(Visit(expr.Operand));
    }
  }
}

