[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA2210:AssembliesShouldHaveValidStrongNames")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1801:AvoidUnusedParameters", Scope = "member", Target = "Microsoft.Zing.ExternalEvent..ctor(System.Boolean)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", Scope = "member", Target = "Microsoft.Zing.Cruncher.Crunch(Microsoft.Zing.ICruncherCallback,Microsoft.Zing.Trace[]&):Microsoft.Zing.CheckerResult")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", Scope = "member", Target = "Microsoft.Zing.Cruncher.Crunch(Microsoft.Zing.Trace[]&):Microsoft.Zing.CheckerResult")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", Scope = "member", Target = "Microsoft.Zing.Cruncher.IterativeCrunch(Microsoft.Zing.ICruncherCallback,Microsoft.Zing.Trace[]&):Microsoft.Zing.CheckerResult")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", Scope = "member", Target = "Microsoft.Zing.Cruncher.IterativeCrunch(Microsoft.Zing.Trace[]&):Microsoft.Zing.CheckerResult")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1801:AvoidUnusedParameters", Scope = "member", Target = "Microsoft.Zing.HeapCanonicalizer+IncrementalAlgorithm.OnStart():System.Void")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", Scope = "member", Target = "Microsoft.Zing.RefinementChecker.Crunch(Microsoft.Zing.RefinementTrace&,Microsoft.Zing.ICruncherCallback):Microsoft.Zing.CheckerResult")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline", Scope = "member", Target = "Microsoft.Zing.TableEntry..cctor()")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", Scope = "member", Target = "Microsoft.Zing.RefinementChecker..ctor(System.String,System.String,System.Boolean,Microsoft.Zing.RefinementKind)")]

//
// TODO: remove this FxCop bug workaround in Whidbey RTM
//
// In Whidbey beta 2, FxCop will fail unless there is at least one error
// or warning in the target assembly. So we introduce this bug, and arrange
// for it to be treated as a warning, while all other FxCop defects are
// treated as errors.
//
namespace Microsoft.WorkaroundForFxCopBug
{
    public class Bar
    {
    }
}