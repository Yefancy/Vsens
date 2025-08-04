# 传感器命令格式说明 (Sensor Command Format)

## 🎯 更新内容

根据Agent端的prompt格式，已对传感器命令进行以下更新：

### ✅ 参数名称更新
- `showVisualization` → `show_visualization`
- 新增 `show_data_graph` 参数
- `parent` 参数保持在 `parameters` 中（更符合参数化设计）
- 删除 `scale` 参数支持（用户很少调整传感器大小）

### 📋 完整的JSON格式

```json
{
    "target": "OPTICAL-1",  // 传感器对象命名格式：SENSORNAME-NUMBER
    "action": "set_sensor",
    "parameters": {
        "sensor_type": "OPTICAL",
        "position": [0, 0, 0],
        "rotation": [0, 0, 0],
        "parent": "bowl",       // 父对象，空字符串表示全局创建
        
        // 传感器可视化选项
        "show_visualization": "true",
        "show_data_graph": "false",

        // DISTANCE传感器特有参数（当sensor_type不是DISTANCE时可省略）
        "validDistance": 5.0,        // 可检测距离
        "lookDirection": [0, 0, 1]   // 检测方向
    }
}
```

## 🔧 支持的传感器类型

### OPTICAL传感器
```json
{
    "target": "OPTICAL-1",
    "action": "set_sensor",
    "parameters": {
        "sensor_type": "OPTICAL",
        "position": [0, 1.5, 2],
        "rotation": [0, 45, 0],
        "parent": "", // 空字符串表示全局创建
        "show_visualization": "true",
        "show_data_graph": "false"
    }
}
```

### DISTANCE传感器
```json
{
    "target": "DISTANCE-1", 
    "action": "set_sensor",
    "parameters": {
        "sensor_type": "DISTANCE",
        "position": [-2, 1, 0],
        "rotation": [0, -90, 0],
        "parent": "Agent", // 附加到Agent对象
        "show_visualization": "true",
        "show_data_graph": "false",
        "validDistance": 10.0,
        "lookDirection": [-1, 0, 0]
    }
}
```

## 🎮 使用场景

### 创建新传感器
- `target` 可以是任意名称，会自动生成传感器对象
- `parent` 在parameters中，为空或null表示在全局创建

### 修改现有传感器
- `target` 必须是场景中存在的传感器对象名称
- 如果指定的`target`不存在，会创建新传感器

### 参数说明
- `sensor_type`: 必需参数，指定传感器类型
- `position`, `rotation`: 可选，传感器的变换参数
- `parent`: 可选，父对象名称，空字符串表示全局创建
- `show_visualization`: 可选，是否显示传感器可视化
- `show_data_graph`: 可选，是否显示数据图表
- `validDistance`: 仅DISTANCE传感器，指定检测距离
- `lookDirection`: 仅DISTANCE传感器，指定检测方向

## 🧪 测试方法

使用 `SensorCommandTest.cs` 脚本中的Context Menu测试：
- **Test Create OPTICAL Sensor**: 测试光学传感器创建
- **Test Create DISTANCE Sensor**: 测试距离传感器创建
- **Test Modify Existing Sensor**: 测试传感器修改
- **Show All Sensors**: 显示所有传感器
- **Clear All Test Sensors**: 清理测试传感器

## 📝 实现细节

### WsClient.ControlObject 更新
- 保持原有结构，parent参数在parameters字典中处理

### ControlManager 更新
- `HandleSetSensorAction()`: 处理传感器命令的主要方法
- `ApplySensorSpecificParameters()`: 应用传感器特定参数，包括parent参数处理
- `ApplyTransformParameters()`: 应用变换参数（移除了scale支持）

### 错误处理
- 详细的调试日志输出
- 参数验证和类型转换
- 异常捕获和错误恢复

## 🎉 完成状态

✅ 参数名称更新完成  
✅ parent字段保持在parameters中（更符合参数化设计）  
✅ 删除scale参数支持  
✅ 添加show_data_graph参数  
✅ 更新测试脚本  
✅ 完整的错误处理和日志记录  

现在AI Agent可以使用统一的参数化JSON格式来创建和管理虚拟传感器！
