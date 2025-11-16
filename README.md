# EasytierUptime-CE

一个社区版的EasytierUptime网页监视器

<!-- PROJECT SHIELDS -->

[![Contributors][contributors-shield]][contributors-url]
[![Forks][forks-shield]][forks-url]
[![Stargazers][stars-shield]][stars-url]
[![Issues][issues-shield]][issues-url]
[![GPL-3.0 License][license-shield]][license-url]
[![LinkedIn][linkedin-shield]][linkedin-url]

<!-- PROJECT LOGO -->
<br />

<p align="center">
  <a href="https://github.com/mxzyTeam/EasytierUptime-CE">
    <img src="images/logo.png" alt="Logo" width="480" height="480">
  </a>

  <h3 align="center">EasytierUptime-CE</h3>
  <p align="center">
    一个社区版的EasytierUptime网页监视器
    <br />
    <a href="https://github.com/mxzyTeam/EasytierUptime-CE"><strong>探索本项目的文档 »</strong></a>
    <br />
    <br />
    <a href="https://github.com/mxzyTeam/EasytierUptime-CE">查看Demo</a>
    ·
    <a href="https://github.com/mxzyTeam/EasytierUptime-CE/issues">报告Bug</a>
    ·
    <a href="https://github.com/mxzyTeam/EasytierUptime-CE/issues">提出新特性</a>
  </p>

</p>


 本篇README.md面向开发者
 
## 目录

- [上手指南](#上手指南)
  - [开发前的配置要求](#开发前的配置要求)
  - [安装步骤](#安装步骤)
- [文件目录说明](#文件目录说明)
- [开发的架构](#开发的架构)
- [使用到的框架](#使用到的框架)
- [贡献者](#贡献者)
  - [如何参与开源项目](#如何参与开源项目)
- [版本控制](#版本控制)
- [作者](#作者)
- [鸣谢](#鸣谢)

### 上手指南
###### 开发前的配置要求

- python 3+
- php 7+
- MySQL 5+

###### **安装步骤**

1. 克隆本项目：
```sh
git clone https://github.com/mxzyTeam/EasytierUptime-CE.git
```
2. 新建一个 PHP 网站，并将此项目移入网站文件夹。

3. 新建名为 `et` 的数据库并进入 `et` 数据库：
```sql
USE et;
```

4. 使用以下命令创建一个汇总表：
```sql
CREATE TABLE `monitor_tables` (
  `id` int(11) NOT NULL AUTO_INCREMENT COMMENT '主键',
  `table_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '分表名（如 monitor_202511）',
  `create_time` datetime NOT NULL COMMENT '分表创建时间',
  `update_time` datetime NOT NULL COMMENT '分表最后更新时间（用于快速定位最新数据）',
  PRIMARY KEY (`id`),
  UNIQUE KEY `table_name` (`table_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='分表元信息表';
```

5. 检查是否创建成功：
```sql
DESCRIBE monitor_tables;
```
或者简写
```sql
DESC monitor_tables;
```

6. 创建一个 Python 网站 (方便启停脚本)
端口若必须填写则随意，请使用 `pip install <模块>` 安装模块，或者先创建完毕，在右侧点击模块进行安装
```python
pip install pymysql requests schedule
```
安装 pymysql requests schedule 这些模块
若下载太慢，你可以使用清华源加速：
```python
pip install -i https://pypi.tuna.tsinghua.edu.cn/simple pymysql requests schedule
```

7. 安装完毕后，启动 Python 网站，在10秒左右你就可以查看服务器列表了

### 文件目录说明
```
EasytierUptime-CE               # 此项目
│  1.json                       # 用于测试的json数据
│  index.html                   # 默认页面  用来显示服务器列表
│  LICENSE                      # GNU v3
│  node-detail.html             # 显示详细信息的页面
│  README.md                    # 你正在看的文档
│
├─api                           # api调用文件夹
│      get-node-detail.php      # 调用详细信息的php
│      get-node-list.php        # 调用服务器列表信息的php，筛选翻页处理
│
├─cdn.bootcdn.net               # 第三方库静态资源
│  └─ajax
│      └─libs
│          ├─echarts
│          │   └─5.4.3
│          │       └─echarts.min.js
│          └─font-awesome
│              └─6.4.0
│                  ├─css
│                  │   └─all.min.css
│                  └─webfonts
│
├─css                           # 层叠样式表
│      index.css                # index.html 的层叠样式表
│      node-detail.css          # node-detail.html 的层叠样式表
│
├─images                        # 图像文件夹
│      logo.png                 # 本项目的logo
|      logo.psd                 # LOGO 的PSD文件
│
├─js                            # JavaScript 文件夹
│      index.js                 # index.html 的 JavaScript 脚本文件
│      node-detail.js           # node-detail.html 的 JavaScript 脚本文件
│
└─pythoncode                    # Python 代码文件夹
        update_monitor.py       # 用来定时半分钟更新数据库的 Python 脚本
```

### 使用到的框架

- [Python](https://python.org)
- [PHP](https://www.php.net/)
- [MySQL](https://www.mysql.com/)

### 贡献者

<p align="left">
  <a href="https://github.com/mxzyTeam/EasytierUptime-CE/graphs/contributors">
    <img src="https://contrib.rocks/image?repo=mxzyTeam/EasytierUptime-CE" alt="Contributors"/>
  </a>
</p>

> 以上为自动生成的贡献者头像墙，点击可查看所有贡献者。

#### 如何参与开源项目

贡献使开源社区成为一个学习、激励和创造的绝佳场所。你所作的任何贡献都是**非常感谢**的。

1. Fork 本项目
2. 创建你的功能分支（`git checkout -b feature/AmazingFeature`）
3. 提交你的更改（`git commit -m '添加一些新特性'`）
4. 推送到你的分支（`git push origin feature/AmazingFeature`）
5. 创建一个 Pull Request

### 版本控制

该项目使用Git进行版本管理。您可以在repository参看当前可用版本。

### 作者

lost-sky-cn@qq.com

QQ:3217863238  &ensp; qq群:1073447360    

 *您也可以在贡献者名单中参看所有参与该项目的开发者。*

### 版权说明

该项目签署了GPL-3.0 license 授权许可，详情请参阅 [LICENSE](https://github.com/mxzyTeam/EasytierUptime-CE/blob/main/LICENSE)

### 鸣谢

- [GitHub Emoji Cheat Sheet](https://www.webpagefx.com/tools/emoji-cheat-sheet)
- [Img Shields](https://shields.io)
- [Choose an Open Source License](https://choosealicense.com)
- [GitHub Pages](https://pages.github.com)
- [Animate.css](https://daneden.github.io/animate.css)
- [Python](https://python.org)
- [PHP](https://www.php.net/)
- [MySQL](https://www.mysql.com/)
- [Font Awesome](https://fontawesome.com/)
- 各大AI文本大模型

<!-- links -->
[your-project-path]:mxzyTeam/EasytierUptime-CE

[contributors-shield]: https://img.shields.io/github/contributors/mxzyTeam/EasytierUptime-CE.svg?style=flat-square
[contributors-url]: https://github.com/mxzyTeam/EasytierUptime-CE/graphs/contributors

[forks-shield]: https://img.shields.io/github/forks/mxzyTeam/EasytierUptime-CE.svg?style=flat-square
[forks-url]: https://github.com/mxzyTeam/EasytierUptime-CE/network/members

[stars-shield]: https://img.shields.io/github/stars/mxzyTeam/EasytierUptime-CE.svg?style=flat-square
[stars-url]: https://github.com/mxzyTeam/EasytierUptime-CE/stargazers

[issues-shield]: https://img.shields.io/github/issues/mxzyTeam/EasytierUptime-CE.svg?style=flat-square
[issues-url]: https://github.com/mxzyTeam/EasytierUptime-CE/issues

[license-shield]: https://img.shields.io/github/license/mxzyTeam/EasytierUptime-CE.svg?style=flat-square
[license-url]: https://github.com/mxzyTeam/EasytierUptime-CE/blob/master/LICENSE

[linkedin-shield]: https://img.shields.io/badge/QQ_Group-1073447360-blue.svg?logo=qq
[linkedin-url]: https://qm.qq.com/q/ToyHu6G4g0