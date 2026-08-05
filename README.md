# jmuwp — 禁漫 Windows Phone / UWP 客户端

经典 UWP (C#/XAML) 客户端。里程碑 1（API 控制台验证）✅ 完成；里程碑 2（UWP 界面）进行中。

## 结构

```
src/Jm.Core      netstandard1.4 类库  —— API 核心，UWP 工程引用其编译后的 DLL
  JmCrypto.cs        token 生成 + 响应 AES 解密
  JmClient.cs        HttpClient 封装、域名轮换、端点(search/album/chapter)
  JmModels.cs        强类型模型(Newtonsoft) + id 数字/字符串容错转换器
  JmImageScramble.cs 图片乱序还原：分段公式 + 条带坐标(平台无关)
tools/Jm.Cli     net10 控制台      —— 联网验证用
tests/Jm.Core.Tests  xUnit          —— 18 个离线测试(加密/分段/重排/模型)全过
app/Jm.Uwp       经典 UWP App       —— 搜索页 + 详情页(VS2017 编译, 最低版本 10240)
```

两个解决方案，**分属两套工具链，不要混用**：
- `jmuwp.slnx` —— Core/CLI/Tests，用 **dotnet**（.NET 10 SDK）构建测试。
- `Jm.Uwp.sln` —— 只含 UWP 工程，用 **VS2017** 打开。它引用 `lib/Jm.Core.dll`（二进制），
  **不含 Jm.Core 工程**，因为 VS2017 的老 MSBuild 编不了 SDK 风格的 netstandard 工程。

## 为什么 Core 是 netstandard1.4 而非 2.0

netstandard2.0 需要 UWP **最低 16299**，而 Windows 10 Mobile 最高只到 **15254** —— 用 2.0 就
彻底失去手机真机支持。1.4 官方对应 UWP 10240，初代 Win10 Mobile 都能装。**别升回 2.0。**

## 构建（顺序很重要）

```powershell
# 1) 先用 dotnet 编 Core，会自动把 Jm.Core.dll 拷到仓库根 lib\
dotnet build src/Jm.Core
dotnet test  tests/Jm.Core.Tests   # 22 个离线测试  ✅

# 2) 再编 UWP（VS2017 的 MSBuild 15.0；VS2026 已砍掉经典 UWP）
& "C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe" `
    app\Jm.Uwp\Jm.Uwp.csproj /t:Restore,Build /p:Configuration=Debug /p:Platform=x86
```

日常开发：用 **VS2017** 打开 `Jm.Uwp.sln`，F5 部署到本机/手机。
**改了 Core 之后**记得重跑 `dotnet build src/Jm.Core` 刷新 `lib\Jm.Core.dll`，VS 再重新生成 UWP。

## API 关键事实（已对齐上游 jmcomic，2026-06 核对）

- **token**     = `md5(ts + "18comicAPPContent")`
- **tokenparam**= `"{ts},{version}"`，version 当前 `2.0.26`
- **响应解密**  : `data` 字段 = base64 → AES-ECB-256 解密（key = `md5(ts + "185Hcomic3PAPP7R")`）→ 去 PKCS7
- 同一次请求**自始至终用同一个 ts**（token 和解密 key 都靠它）
- 当前 API 域名：`www.cdnaspa.club` / `.vip`、`www.cdnplaystation6.cc` / `.vip`
  （域名换得很勤；真实客户端还会从 BytePlus TOS `newsvr-2025.txt` 动态拉取最新列表，**待实现**）

## 运行验证

```powershell
dotnet run --project tools/Jm.Cli -- token            # 离线自检加密(不联网)  ✅ 已通过
dotnet run --project tools/Jm.Cli -- search love      # 搜索
dotnet run --project tools/Jm.Cli -- album 123456     # 专辑详情
dotnet run --project tools/Jm.Cli -- chapter 123456   # 章节图片列表
```

应对域名被网络层拦截：
```powershell
$env:JM_PROXY = "http://127.0.0.1:7890"     # 走代理
$env:JM_DOMAIN = "www.xxx.vip"              # 覆盖域名(逗号分隔多个)
```

## 当前状态 / 已知问题

- ✅ 工程骨架、加密、客户端、控制台 —— build 干净，加密离线自检通过。
- ⚠️ **从开发机直连 JM API 失败**：Cloudflare 回源到诱饵 Apache 一律 404 / TLS reset，
  属地区网络层拦截，非代码问题。需在可用网络或加代理（`JM_PROXY`）下做端到端验证。

## 待办

- [x] search/album/chapter 强类型模型
- [x] 图片乱序还原算法（坐标计算 + 单测）
- [x] UWP 工程骨架 + 搜索页/详情页/阅读器页（VS2017 全部编译通过）
- [x] 阅读器 ReaderPage：拉 chapter 图 → 下载 → JmImageScramble 还原 → 竖向滚动显示
- [x] 图片乱序还原 UWP 实装（JmImageLoader：ImageStrip 坐标 blit 到 WriteableBitmap）
- [x] 动态域名拉取（发布页/BytePlus `newsvr-2025.txt`，空 ts + secret `diosfjckwpqpdfjkvnqQjsik`，取 JSON 的 `Server` 字段）
- [x] 扩充内置备用域名 + 搜索失败自动拉新域名重试 + 启动后台静默刷新
- [x] 封面预览 + 网格/列表视图切换（MainPage）+ 详情页封面
- [x] 阅读器：自动对齐窗口宽度 + 缩放 + 顶部菜单(目录/书签/下载) + 底部跳页进度条
- [x] 下载本章到本地（乱序图还原后存 JPEG）
- [x] 书签：阅读器添加 + BookmarksPage 查看/删除/跳转回原页
- [x] 首页收藏/历史（带图卡片，点击进详情）+ 详情页收藏按钮
- [x] 设置页（图片并发数、更新域名、清空历史）
- [x] 多线程：阅读器/下载按并发数(默认4)同时拉图，提速
- [x] 真机部署（Win10 Mobile 15254 ARM32，WDP 推送）+ 端到端跑通（搜索/封面/阅读/webp 出图）
- [x] WebP 解码：复用 Universal.WebP 原生 WinRT 组件（ARM32 可用，必须 Release 包）
- [ ] 图片磁盘缓存、预加载下一页
- [ ] 正式应用图标（当前 Assets 是 1x1 占位 PNG）
