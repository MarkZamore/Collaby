"""
Описание инструментов, которые `api-ai` может вернуть фронтенду.
Исполнение происходит только во frontend через PlatformContext.
"""

TOOLS: list[dict] = [
    {
        "name": "create_workspace",
        "description": "Создать новое рабочее пространство.",
        "parameters": {
            "name": {"type": "string", "description": "Название воркспейса", "required": True},
        },
    },
    {
        "name": "select_workspace",
        "description": "Переключиться на существующий воркспейс по ID.",
        "parameters": {
            "workspaceId": {
                "type": "string",
                "description": "ID воркспейса из контекста. Не выдумывай новые ID.",
                "required": True,
            },
        },
    },
    {
        "name": "create_project",
        "description": "Создать проект в текущем или указанном воркспейсе.",
        "parameters": {
            "name": {"type": "string", "description": "Название проекта", "required": True},
            "description": {"type": "string", "description": "Описание проекта", "required": False},
            "workspaceId": {
                "type": "string",
                "description": "ID воркспейса из контекста. Если не указан, будет использован текущий.",
                "required": False,
            },
        },
    },
    {
        "name": "select_project",
        "description": "Переключиться на существующий проект по ID.",
        "parameters": {
            "projectId": {
                "type": "string",
                "description": "ID проекта из контекста. Разрешается резолвить по названию проекта из списка.",
                "required": True,
            },
        },
    },
    {
        "name": "delete_project",
        "description": "Удалить проект в текущем или указанном воркспейсе.",
        "parameters": {
            "workspaceId": {"type": "string", "description": "ID воркспейса", "required": False},
            "projectId": {"type": "string", "description": "ID проекта", "required": False},
        },
    },
    {
        "name": "create_column",
        "description": "Создать одну колонку на текущей доске.",
        "parameters": {
            "name": {
                "type": "string",
                "description": "Название колонки, например Backlog, В работе или Готово.",
                "required": True,
            },
            "color": {"type": "string", "description": "HEX-цвет колонки", "required": False},
            "isFinal": {"type": "boolean", "description": "Финальная ли колонка", "required": False},
        },
    },
    {
        "name": "bulk_create_columns",
        "description": "Создать несколько колонок одним вызовом. Используй этот инструмент, если колонок две и больше.",
        "parameters": {
            "columns": {
                "type": "array",
                "description": "Массив объектов вида { name, color?, isFinal? }.",
                "required": True,
            },
        },
    },
    {
        "name": "reorder_columns",
        "description": "Изменить порядок колонок на доске.",
        "parameters": {
            "columnIds": {
                "type": "array",
                "description": "Массив column/status ID в новом порядке.",
                "required": True,
            },
        },
    },
    {
        "name": "create_task",
        "description": "Создать одну задачу. Если задач несколько, используй bulk_create_tasks.",
        "parameters": {
            "title": {"type": "string", "description": "Название задачи", "required": True},
            "description": {"type": "string", "description": "Описание задачи", "required": False},
            "statusId": {
                "type": "string",
                "description": "ID колонки из текущего контекста. Если пользователь указывает колонку по названию, сначала найди её statusId.",
                "required": False,
            },
            "priority": {
                "type": "string",
                "description": "Приоритет: Low, Medium, High или Critical.",
                "required": False,
            },
            "dueDate": {"type": "string", "description": "Срок в формате YYYY-MM-DD", "required": False},
            "assigneeId": {"type": "string", "description": "ID исполнителя из контекста", "required": False},
        },
    },
    {
        "name": "bulk_create_tasks",
        "description": "Создать несколько задач одним вызовом. Используй этот инструмент, если задач две и больше.",
        "parameters": {
            "tasks": {
                "type": "array",
                "description": "Массив объектов вида { title, statusId?, priority?, description?, dueDate?, assigneeId? }.",
                "required": True,
            },
        },
    },
    {
        "name": "update_task",
        "description": "Обновить существующую задачу.",
        "parameters": {
            "taskId": {"type": "string", "description": "ID задачи", "required": True},
            "title": {"type": "string", "description": "Новое название", "required": False},
            "description": {"type": "string", "description": "Новое описание", "required": False},
            "priority": {
                "type": "string",
                "description": "Новый приоритет: Low, Medium, High или Critical.",
                "required": False,
            },
            "dueDate": {"type": "string", "description": "Новый срок", "required": False},
        },
    },
    {
        "name": "delete_task",
        "description": "Удалить задачу.",
        "parameters": {
            "taskId": {"type": "string", "description": "ID задачи", "required": True},
        },
    },
    {
        "name": "move_task",
        "description": "Переместить задачу в другую колонку.",
        "parameters": {
            "taskId": {
                "type": "string",
                "description": "ID задачи из контекста. Если у пользователя в тексте только название, сначала найди соответствующую задачу.",
                "required": True,
            },
            "statusId": {
                "type": "string",
                "description": "ID целевой колонки из контекста.",
                "required": True,
            },
        },
    },
    {
        "name": "create_subtask",
        "description": "Добавить подзадачу к задаче.",
        "parameters": {
            "taskId": {"type": "string", "description": "ID родительской задачи", "required": True},
            "title": {"type": "string", "description": "Название подзадачи", "required": True},
        },
    },
    {
        "name": "create_label",
        "description": "Создать новую метку в текущем проекте.",
        "parameters": {
            "name": {"type": "string", "description": "Название метки", "required": True},
            "color": {"type": "string", "description": "HEX-цвет метки", "required": False},
        },
    },
    {
        "name": "assign_label",
        "description": "Назначить существующую метку задаче.",
        "parameters": {
            "taskId": {"type": "string", "description": "ID задачи", "required": True},
            "labelId": {"type": "string", "description": "ID существующей метки", "required": True},
        },
    },
    {
        "name": "remove_label",
        "description": "Снять метку с задачи.",
        "parameters": {
            "taskId": {"type": "string", "description": "ID задачи", "required": True},
            "labelId": {"type": "string", "description": "ID метки", "required": True},
        },
    },
    {
        "name": "delete_label",
        "description": "Удалить метку из проекта.",
        "parameters": {
            "labelId": {"type": "string", "description": "ID метки", "required": True},
        },
    },
    {
        "name": "add_comment",
        "description": "Добавить комментарий к задаче.",
        "parameters": {
            "taskId": {"type": "string", "description": "ID задачи", "required": True},
            "text": {"type": "string", "description": "Текст комментария", "required": True},
        },
    },
    {
        "name": "invite_member",
        "description": "Пригласить участника в воркспейс.",
        "parameters": {
            "userId": {"type": "string", "description": "ID пользователя из контекста", "required": True},
            "workspaceId": {"type": "string", "description": "ID воркспейса", "required": False},
            "role": {"type": "string", "description": "Member или Admin", "required": False},
        },
    },
    {
        "name": "update_member_role",
        "description": "Изменить роль участника воркспейса.",
        "parameters": {
            "userId": {"type": "string", "description": "ID участника", "required": True},
            "role": {"type": "string", "description": "Новая роль: Member или Admin", "required": True},
            "workspaceId": {"type": "string", "description": "ID воркспейса", "required": False},
        },
    },
    {
        "name": "add_board_sticker",
        "description": "Включить стикер на доске.",
        "parameters": {
            "stickerId": {"type": "string", "description": "ID стикера из каталога доски", "required": True},
        },
    },
    {
        "name": "remove_board_sticker",
        "description": "Убрать стикер с доски.",
        "parameters": {
            "stickerId": {"type": "string", "description": "ID стикера", "required": True},
        },
    },
    {
        "name": "get_board_summary",
        "description": "Вернуть текстовую сводку по текущей доске: количество задач, распределение по колонкам и просроченные элементы.",
        "parameters": {},
    },
]


def get_tool_schemas() -> list[dict]:
    """Преобразует локальное описание инструментов в provider-agnostic JSON schema."""

    declarations: list[dict] = []
    for tool in TOOLS:
        declaration = {
            "name": tool["name"],
            "description": tool["description"],
            "parameters": {
                "type": "object",
                "properties": {},
                "required": [],
            },
        }

        for param_name, param_info in tool.get("parameters", {}).items():
            param_type = param_info.get("type", "string")
            description = param_info.get("description", "")

            if param_type == "array":
                if param_name == "columns":
                    items = {
                        "type": "object",
                        "properties": {
                            "name": {"type": "string", "description": "Название колонки"},
                            "color": {"type": "string", "description": "HEX-цвет колонки"},
                            "isFinal": {"type": "boolean", "description": "Финальная ли колонка"},
                        },
                    }
                elif param_name == "tasks":
                    items = {
                        "type": "object",
                        "properties": {
                            "title": {"type": "string", "description": "Название задачи"},
                            "description": {"type": "string", "description": "Описание задачи"},
                            "statusId": {"type": "string", "description": "ID колонки"},
                            "priority": {"type": "string", "description": "Приоритет"},
                            "dueDate": {"type": "string", "description": "Срок"},
                            "assigneeId": {"type": "string", "description": "ID исполнителя"},
                        },
                    }
                else:
                    items = {"type": "string"}

                declaration["parameters"]["properties"][param_name] = {
                    "type": "array",
                    "description": description,
                    "items": items,
                }
            else:
                declaration["parameters"]["properties"][param_name] = {
                    "type": param_type,
                    "description": description,
                }

            if param_info.get("required"):
                declaration["parameters"]["required"].append(param_name)

        declarations.append(declaration)

    return declarations
