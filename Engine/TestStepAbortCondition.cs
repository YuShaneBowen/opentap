using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OpenTap
{
    [Flags]
    public enum TestStepAbortCondition
    {
        /// <summary> If a step completes with verdict 'Fail', stop execution of any subsequent steps at this level, and return control to the parent step. </summary>
        [Display("Inherit", "Inherit behavior from the parent step. If no parent step exist or specify a behavior, the Engine setting 'Abort Run If' is used.")]
        Inherit = 1,
        /// <summary> If a step completes with verdict 'Fail', stop execution of any subsequent steps at this level, and return control to the parent step. </summary>
        [Display("Break on fail", "If a step completes with verdict 'Fail', stop execution of any subsequent steps at this level, and return control to the parent step.")]
        BreakOnFail = 2,
        /// <summary> If a step completes with verdict 'Error', stop execution of any subsequent steps at this level, and return control to the parent step. </summary>
        [Display("Break on Error", "If a step completes with verdict 'Error', stop execution of any subsequent steps at this level, and return control to the parent step.")]
        BreakOnError = 4,
        /// <summary> If a step completes with verdict 'Fail', the test step should be re-run. </summary>
        [Display("Retry on Fail", "If a step completes with verdict 'Fail', the test step should be re-run.")]
        RetryOnFail = 8,
        /// <summary> If a step completes with verdict 'Error', the test step should be re-run. </summary>
        [Display("Retry on Error", "If a step completes with verdict 'Error', the test step should be re-run.")]
        RetryOnError = 16,
        /// <summary> If a step completes with verdict 'Inclusive' the step should break execution.</summary>
        [Display("Break on Inconclusive")]
        BreakOnInconclusive = 32,
        /// <summary> If a step completes with verdict 'Inclusive' the step should retry a number of times.</summary>
        [Display("Retry on Inconclusive")]
        RetryOnInconclusive = 64
    }

    public static class AbortCondition
    {
        public static void SetAbortCondition(this ITestStep step, TestStepAbortCondition condition)
        {
            
        }
        
        public static TestStepAbortCondition GetAbortCondition(this ITestStep step)
        {
            throw new NotImplementedException();
        }
        
        public static void SetRetries(this ITestStep step, uint retries)
        {
            throw new NotImplementedException();
        }

        public static uint GetRetries(this ITestStep step)
        {
            throw new NotImplementedException();
        }
    }

    internal class AbortConditionTypeDataProvider// : IStackedTypeDataProvider
    {
        class VirtualMember<T> : IMemberData
        {
            public IEnumerable<object> Attributes { get; set; }
            public string Name { get; set; }
            public ITypeData DeclaringType { get; set; }
            public ITypeData TypeDescriptor { get; set; }
            public bool Writable { get; set; }
            public bool Readable { get; set; }

            public object DefaultValue;
            
            

            ConditionalWeakTable<object, object> dict = new ConditionalWeakTable<object, object>();

            public void SetValue(object owner, object value)
            {
                dict.Add(owner, value);
            }

            public object GetValue(object owner)
            {
                if (dict.TryGetValue(owner, out object value))
                    return value;
                return DefaultValue;
            }
        }
        class TestStepTypeData : ITypeData
        {
            static VirtualMember<TestStepAbortCondition> AbortCondition = new VirtualMember<TestStepAbortCondition>
            {
                Name = "AbortCondition",
                DefaultValue = TestStepAbortCondition.Inherit,
                Attributes = new []{new DisplayAttribute("Abort Condition")},
                DeclaringType = TypeData.FromType(typeof(TestStepTypeData)),
                Readable = true,
                Writable =  true,
                TypeDescriptor = TypeData.FromType(typeof(TestStepAbortCondition))
            };
            
            static VirtualMember<uint> Retries = new VirtualMember<uint>
            {
                Name = "Retries",
                DefaultValue = 0,
                Attributes = new []{new DisplayAttribute("Retrues")},
                DeclaringType = TypeData.FromType(typeof(TestStepTypeData)),
                Readable = true,
                Writable =  true,
                TypeDescriptor = TypeData.FromType(typeof(uint))
            };

            static IMemberData[] extraMembers =  {AbortCondition, Retries};
            public TestStepTypeData(ITypeData innerType)
            {
                this.innerType = innerType;
            }
            readonly ITypeData innerType;
            public IEnumerable<object> Attributes => innerType.Attributes;
            public string Name => innerType.Name;
            public ITypeData BaseType => innerType.BaseType;
            public IEnumerable<IMemberData> GetMembers()
            {
                return innerType.GetMembers().Concat(extraMembers);
            }

            public IMemberData GetMember(string name)
            {
                if (name == AbortCondition.Name) return AbortCondition;
                if (name == Retries.Name) return Retries;
                return innerType.GetMember(name);
            }

            public object CreateInstance(object[] arguments)
            {
                return innerType.CreateInstance(arguments);
            }

            public bool CanCreateInstance => innerType.CanCreateInstance;
        }
        public ITypeData GetTypeData(string identifier, TypeDataProviderStack stack)
        {
            var subtype = stack.GetTypeData(identifier);
            if (subtype.DescendsTo(typeof(ITestStep)))
            {
                return new TestStepTypeData(subtype);
            }

            return subtype;
        }

        public ITypeData GetTypeData(object obj, TypeDataProviderStack stack)
        {
            if (obj is ITestStep step)
            {
                var subtype = stack.GetTypeData(obj);
                return new TestStepTypeData(subtype);
            }

            return null;
        }

        public double Priority { get; } = 10;
    }
}