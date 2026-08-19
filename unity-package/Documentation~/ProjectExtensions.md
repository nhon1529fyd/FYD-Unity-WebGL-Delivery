# Project extensions

Phần lõi FYD WebGL không tham chiếu namespace hoặc asset riêng của bất kỳ game nào.
Một project có thể thêm kiểm tra riêng bằng một file Editor:

```csharp
using System.Collections.Generic;
using FYD.WebGLTools;

public sealed class MyGameWebGLChecks : IFYDWebGLProjectExtension
{
    public bool PrepareForBuild(out string error)
    {
        error = string.Empty;
        return true;
    }

    public void AppendChecks(List<FYDCheckItem> items)
    {
        items.Add(new FYDCheckItem(
            "Nội dung riêng của game đã sẵn sàng",
            FYDCheckStatus.Pass,
            "Mô tả kết quả kiểm tra."));
    }
}
```

Lớp phải nằm trong Editor assembly, có constructor không tham số và không được là
abstract. Package dùng `TypeCache` để tự phát hiện extension sau mỗi domain reload.
