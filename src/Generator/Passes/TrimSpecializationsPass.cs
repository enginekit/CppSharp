﻿using System;
using System.Collections.Generic;
using System.Linq;
using CppSharp.AST;
using CppSharp.AST.Extensions;

namespace CppSharp.Passes
{
    public class TrimSpecializationsPass : TranslationUnitPass
    {
        public override bool VisitASTContext(ASTContext context)
        {
            var result = base.VisitASTContext(context);
            foreach (var template in templates)
                CleanSpecializations(template);
            return result;
        }

        public override bool VisitClassDecl(Class @class)
        {
            if (!base.VisitClassDecl(@class))
                return false;

            if (@class.IsDependent)
                templates.Add(@class);
            else
                foreach (var @base in @class.Bases.Where(b => b.IsClass))
                {
                    var specialization = @base.Class as ClassTemplateSpecialization;
                    if (specialization != null)
                        specializations.Add(specialization);
                }

            return true;
        }

        public override bool VisitFunctionDecl(Function function)
        {
            if (!base.VisitFunctionDecl(function))
                return false;

            if (function.IsGenerated)
            {
                Action<ClassTemplateSpecialization> add =
                    s =>
                    {
                        if (internalSpecializations.Contains(s))
                            internalSpecializations.Remove(s);
                        specializations.Add(s);
                    };
                ASTUtils.CheckTypeForSpecialization(function.OriginalReturnType.Type,
                    function, add, Context.TypeMaps);
                foreach (var parameter in function.Parameters)
                    ASTUtils.CheckTypeForSpecialization(parameter.Type, function,
                        add, Context.TypeMaps);
            }

            return true;
        }

        public override bool VisitFieldDecl(Field field)
        {
            if (!base.VisitDeclaration(field))
                return false;

            ASTUtils.CheckTypeForSpecialization(field.Type, field,
                s =>
                {
                    if (!specializations.Contains(s))
                        internalSpecializations.Add(s);
                }, Context.TypeMaps, true);
            return true;
        }

        private void CleanSpecializations(Class template)
        {
            template.Specializations.RemoveAll(s => !s.IsExplicitlyGenerated &&
                !specializations.Contains(s) && !internalSpecializations.Contains(s));

            foreach (var specialization in template.Specializations.Where(
                s => !s.IsExplicitlyGenerated &&
                (s.Arguments.Any(a =>
                {
                    if (a.Kind != TemplateArgument.ArgumentKind.Declaration &&
                        a.Kind != TemplateArgument.ArgumentKind.Template &&
                        a.Kind != TemplateArgument.ArgumentKind.Type)
                        return true;

                    var type = a.Type.Type.Desugar();
                    if (ASTUtils.IsTypeExternal(template.TranslationUnit.Module, type) ||
                        type.IsPrimitiveType(PrimitiveType.Void))
                        return true;

                    var typeIgnoreChecker = new TypeIgnoreChecker(TypeMaps);
                    type.Visit(typeIgnoreChecker);
                    return typeIgnoreChecker.IsIgnored;
                }) ||
                s.SpecializationKind == TemplateSpecializationKind.ExplicitSpecialization ||
                s is ClassTemplatePartialSpecialization ||
                internalSpecializations.Contains(s))))
                specialization.ExplicitlyIgnore();

            Func<TemplateArgument, bool> allPointers =
                a => a.Type.Type != null && a.Type.Type.IsAddress();
            var groups = (from specialization in template.Specializations
                          group specialization by specialization.Arguments.All(allPointers)
                          into @group
                          select @group).ToList();

            foreach (var group in groups.Where(g => g.Key))
                foreach (var specialization in group.Skip(1))
                    template.Specializations.Remove(specialization);

            for (int i = template.Specializations.Count - 1; i >= 0; i--)
            {
                var specialization = template.Specializations[i];
                if (specialization is ClassTemplatePartialSpecialization &&
                    !specialization.Arguments.All(allPointers))
                    template.Specializations.RemoveAt(i);
            }

            if (!template.IsExplicitlyGenerated &&
                template.Specializations.All(s => s.Ignore))
                template.ExplicitlyIgnore();
        }

        private HashSet<ClassTemplateSpecialization> specializations = new HashSet<ClassTemplateSpecialization>();
        private HashSet<ClassTemplateSpecialization> internalSpecializations = new HashSet<ClassTemplateSpecialization>();
        private HashSet<Class> templates = new HashSet<Class>();
    }
}
