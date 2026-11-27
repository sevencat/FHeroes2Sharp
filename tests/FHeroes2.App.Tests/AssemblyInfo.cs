using Xunit;

// 这批用例共用几处进程级状态：GameAssets.Current 指向的归档（编辑器场景、View World 的合成表按钮都按它取图）
// 与 Settings.Get()（编辑器叠层、界面换皮开关）。并行跑时一个类刚把 Current 换回自己那份、另一个类已经把它
// 关掉，取图就会撞上已关闭的文件句柄。参考实现里这些也是全局单例，所以按类串行即可。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
