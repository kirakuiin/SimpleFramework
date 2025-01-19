# SimpleFramework

一个轻量级的 C# 框架，提供了模块化、事件驱动的架构设计。

## 核心功能

### 1. 领域驱动设计
- **Domain**: 顶层容器，管理整个应用的生命周期
- **System**: 跨实体的业务逻辑实现
- **Model**: 单一职责的数据模型
- **Utility**: 提供底层功能支持

### 2. 事件系统
- 支持域内事件传递和处理
- 提供全局事件总线
- 支持事件注册与注销
- 支持带通知的属性绑定

### 3. 命令与查询分离(CQRS)
- **Command**: 用于修改数据的操作
- **Query**: 用于只读数据的查询
- 支持带返回值的命令执行

### 4. 工具集合
- **DefaultDict**: 带默认值的字典实现
- **DisposableGroup**: 资源管理组
- **EntityComponent**: 实体组件系统
- **NetworkUtil**: 网络工具类
- **UdpBroadcast**: UDP广播工具
  - 支持基础的UDP广播发送和接收
  - 支持定时广播功能
  - 支持持续监听广播
  - 支持泛型消息格式
- **SerializeTool**: JSON序列化工具
- **TimeUtil**: 时间转换工具

### 5. 扩展方法
- **List**: 列表操作扩展
- **Random**: 随机数相关扩展
- **String**: 字符串处理扩展

### 6. 数学相关

- **六边形网格**: 基于 [Red Blob Games](http://www.redblobgames.com/grids/hexagons/) 的六边形网格实现，支持尖角朝上和平边朝上两种布局，可自定义大小和形状。
