using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace WindowTranslator.ComponentModel;

public class ResetableLazy<T>
{

    private Lazy<T> lazy;
    private readonly Func<T> valueFactory;
    private readonly LazyThreadSafetyMode mode;

    public bool IsValueCreated => this.lazy.IsValueCreated;

    public T Value => this.lazy.Value;

    public ResetableLazy(Func<T> valueFactory)
    : this(valueFactory, LazyThreadSafetyMode.ExecutionAndPublication)
    {
    }

    public ResetableLazy(Func<T> valueFactory, bool isThreadSafe)
        : this(valueFactory, isThreadSafe ? LazyThreadSafetyMode.ExecutionAndPublication : LazyThreadSafetyMode.None)
    {
    }

    public ResetableLazy(Func<T> valueFactory, LazyThreadSafetyMode mode)
    {
        this.valueFactory = valueFactory;
        this.mode = mode;
        this.lazy = CreateLazy();
    }

    private Lazy<T> CreateLazy()
        => new(this.valueFactory, this.mode);


    public void Reset()
    {
        if (this.lazy.IsValueCreated)
        {
            Interlocked.Exchange(ref this.lazy, CreateLazy());
        }
    }
}
