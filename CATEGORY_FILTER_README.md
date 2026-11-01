# 分类筛选功能实现说明

## 已完成的工作

### 1. UWP 版本 - 分类筛选功能

#### 核心库更新 (src/Jm.Core/)

**JmModels.cs**
- 新增 `JmCategory` 类，定义了分类常量：
  - `All = "0"` - 全部
  - `Doujinshi = "1"` - 同人志
  - `Single = "2"` - 单行本
  - `Short = "3"` - 短篇
  - `CG = "4"` - CG
  - `Cosplay = "5"` - Cosplay

**JmClient.cs**
- 更新 `SearchAsync` 方法，新增 `category` 参数
- 支持通过 `main_tag` 参数进行分类筛选

#### UI 更新 (app/Jm.Uwp/)

**MainPage.xaml**
- 在搜索页面添加了分类选择器 `CategoryBox`
- 调整了布局，将分类选择器放在排序选择器之前
- 支持的分类：全部、同人志、单行本、短篇、CG、Cosplay

**MainPage.xaml.cs**
- 新增 `CategoryBox_SelectionChanged` 事件处理
- 新增 `GetCategory()` 方法，根据选择返回对应的分类代码
- 更新 `DoSearch()` 方法，传递分类参数到搜索源

**IncrementalSearchSource.cs**
- 构造函数新增 `category` 参数
- 搜索时将分类参数传递给 API

### 2. WP8.1 版本 - 完整移植

创建了全新的 Windows Phone 8.1 Silverlight 项目 (app/Jm.Wp81/)：

**项目结构**
```
app/Jm.Wp81/
├── Jm.Wp81.csproj          # 项目文件
├── App.xaml                # 应用程序定义
├── App.xaml.cs             # 应用程序逻辑
├── MainPage.xaml           # 主页面 UI
├── MainPage.xaml.cs        # 主页面逻辑
├── packages.config         # NuGet 包配置
└── Properties/
    ├── AssemblyInfo.cs     # 程序集信息
    ├── AppManifest.xml     # 应用清单
    └── WMAppManifest.xml   # WP8.1 清单
```

**主要特性**
- ✅ 分类筛选（全部、同人志、单行本、短篇、CG、Cosplay）
- ✅ 排序选择（综合、最新、最多点击、最多爱心、最多图片）
- ✅ 关键词搜索
- ✅ 搜索结果网格显示
- ✅ 收藏页面（预留）
- ✅ 历史页面（预留）
- ✅ 应用栏（书签、设置按钮）

**技术特点**
- 使用 `LongListSelector` 实现网格布局
- 使用 `ListPicker` 实现排序选择
- 使用 `Pivot` 实现页面切换
- 复用 Jm.Core 核心库，保持与 UWP 版本的 API 一致性

## 使用说明

### UWP 版本

1. 打开项目：`app/Jm.Uwp/Jm.Uwp.csproj`
2. 在搜索页面，可以看到新增的"分类"下拉框
3. 选择分类后，输入关键词搜索即可按分类筛选

### WP8.1 版本

1. 需要 Visual Studio 2015 或更高版本（支持 WP8.1 开发）
2. 打开项目：`app/Jm.Wp81/Jm.Wp81.csproj`
3. 需要先编译核心库：
   ```bash
   dotnet build src/Jm.Core/Jm.Core.csproj
   # 将生成的 Jm.Core.dll 复制到 lib/ 目录
   ```
4. 部署到 WP8.1 模拟器或真机

## API 参数说明

搜索接口现在支持以下参数：

- `search_query` - 搜索关键词
- `main_tag` - 分类代码（0=全部, 1=同人志, 2=单行本, 3=短篇, 4=CG, 5=Cosplay）
- `o` - 排序方式（mr=最新, mv=最多点击, tf=最多爱心, mp=最多图片）
- `page` - 页码

## 注意事项

1. **网络问题**：由于 memory 中提到"开发机网络被拦截"，可能需要配置代理才能访问 API
2. **核心库依赖**：WP8.1 项目引用预编译的 `lib/Jm.Core.dll`，需要先编译核心库
3. **TODO 项**：WP8.1 版本中标注了 TODO 的功能（详情页、收藏、历史存储）需要后续完善

## 下一步开发建议

1. 实现 WP8.1 的详情页和阅读器页面
2. 实现本地存储（收藏、历史、下载）
3. 添加图片加载和缓存机制
4. 实现书签功能
5. 添加设置页面（代理配置、图片质量等）
