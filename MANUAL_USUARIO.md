# Manual de Usuario - VannaLight

**Aplicación:** Asistente Industrial de IA  
**Versión:** 1.0  
**Última actualización:** Abril 2026

---

## Índice

1. [Introducción](#1-introducción)
2. [Primeros pasos](#2-primeros-pasos)
3. [Modos de operación](#3-modos-de-operación)
4. [Consultas SQL](#4-consultas-sql)
5. [Documentación PDF](#5-documentación-pdf)
6. [Predicciones ML](#6-predicciones-ml)
7. [Historial](#7-historial)
8. [Gráficos y exportación](#8-gráficos-y-exportación)
9. [Feedback](#9-feedback)
10. [Solución de problemas](#10-solución-de-problemas)

---

## 1. Introducción

VannaLight es un asistente de IA que te permite hacer preguntas sobre tus datos en **lenguaje natural**. El sistema convierte tus preguntas en consultas SQL y devuelve los resultados.

### ¿Qué puede hacer?

| Modo | Descripción | Ejemplo |
|------|-------------|----------|
| **SQL** | Consulta tu base de datos | "¿Cuáles son los top 5 scrap de hoy?" |
| **PDF** | Busca en documentos técnicos | "¿Cómo cambiar el molde?" |
| **ML** | Predicciones con machine learning | "¿Cuánto scrap tendremos mañana?" |

### Requisitos

- Un navegador moderno (Chrome, Firefox, Edge, Safari)
- Conexión al servidor de VannaLight
- Credenciales de acceso (si está configurado)

---

## 2. Primeros pasos

### 2.1 Acceder a la aplicación

1. Abre tu navegador
2. Ingresa la URL proporcionada por tu administrador
3. Verás la pantalla principal con la barra superior

### 2.2 Elementos de la interfaz

```
┌─────────────────────────────────────────────────────────┐
│ [Logo] VANNA LIGHT    │ Modo: SQL │ Estado: Conectado │
├──────────────┬──────────────────────────────────────────┤
│             │                                          │
│  SQL        │  [Barra de contexto]                  │
│  PDF        │  "¿Cuál es el top scrap de hoy?"       │
│  ML         │                                          │
│             │  [Resultado / Tabla / Gráfico]          │
│  ─────────  │                                          │
│  Historial  │  [Input de pregunta...]         [Enviar] │
│             │                                          │
└──────────────┴──────────────────────────────────────────┘
```

### 2.3 Indicadores de estado

| Indicador | Significado |
|----------|-------------|
| 🟢 Verde parpadeante | Conectado al servidor |
| 🔴 Rojo | Desconectado - intenta recargar |
| 🟡 Amarillo | Procesando consulta |

---

## 3. Modos de operación

### Cambiar entre modos

Haz clic en los botones del sidebar izquierdo:

```
┌────────────────┐
│   [●] SQL      │  ← Modo datos/SQL
│   [ ] PDF      │  ← Modo documentos
│   [ ] ML       │  ← Modo predicciones
└────────────────┘
```

### Context Strip

En la parte superior verás información del contexto activo:

```
┌──────────────────────────────────────────────────────────────┐
│ Contexto activo: empresa_x | dominio_erp | Conexión: default │
└──────────────────────────────────────────────────────────────┘
```

**Nota:** Si ves "Sin contexto activo", el sistema usa la configuración por defecto del servidor.

---

## 4. Consultas SQL

### 4.1 Hacer una consulta

1. Asegúrate de estar en modo **SQL** (el botón debe estar activo)
2. Escribe tu pregunta en el campo de texto
3. Presiona **Enviar** o presiona **Enter**

**Ejemplos de preguntas:**

| Pregunta | Descripción |
|----------|------------|
| "¿Cuáles son los top 10 scrap de esta semana?" | Muestra los mayores scrap |
| "¿Cuánta producción tuvimos ayer?" | Totales de producción |
| "¿Cuál fue el downtime por falla?" | Análisis de paradas |
| "¿Scrap por molde este mes?" | Desglose por molde |
| "¿Cuál prensa tiene más scrap?" | Ranking de prensas |

### 4.2 Tipos de resultados

**Tarjetas KPI (cuando hay pocos valores):**

```
┌─────────────┐ ┌─────────────┐ ┌─────────────┐
│ Producción  │ │   Scrap     │ │  Downtime   │
│   12,450    │ │    234      │ │    45 min  │
└─────────────┘ └─────────────┘ └─────────────┘
```

**Tabla de datos (cuando hay muchos valores):**

| Prensa | Scrap Qty | Fecha |
|--------|-----------|-------|
| P-001  | 150       | Hoy  |
| P-002  | 120       | Hoy  |

**Gráfico (para tendencias):**

- Barras: Comparaciones
- Líneas: Tendencias temporales

### 4.3 Tiempo de respuesta

| Tipo de consulta | Tiempo estimado |
|-----------------|----------------|
| Consulta simple | 3-10 segundos |
| Consulta compleja | 10-30 segundos |
| Primera consulta | 15-45 segundos |

---

## 5. Documentación PDF

### 5.1 Activar modo PDF

1. Haz clic en el botón **PDF** del sidebar
2. El indicador superior cambiará a color verde
3. Escribe tu pregunta sobre los documentos

### 5.2 Ejemplos de preguntas

| Pregunta | Busca en |
|----------|----------|
| "¿Cómo instalar el molde X?" | Manuales de instalación |
| "¿Cuáles son los parámetros del prensa Y?" | Guías de operación |
| "¿Qué hacer en caso de falla Z?" | Procedimientos |
| "¿Cuándo cambiar el aceite?" | Mantenimiento |

### 5.3 Resultados

El sistema muestra:
- **Respuesta** con la información encontrada
- **Citas** indicando la fuente (página, sección)
- **Nivel de confianza** (porcentaje)

```
┌────────────────────────────────────────────────────────┐
│ Respuesta encontrada con 87% de confianza            │
├────────────────────────────────────────────────────────┤
│ Para instalar el molde X, siga estos pasos:          │
│ 1. Verifique que la prensa esté apagada              │
│ 2. Retire el molde antiguo...                         │
└────────────────────────────────────────────────────────┘
                        Fuente: Manual_Prensa_v2.pdf - Pág 12
```

---

## 6. Predicciones ML

### 6.1 Activar modo Predicción

1. Haz clic en el botón **ML** del sidebar
2. El indicador superior cambiará a color morado
3. Escribe tu pregunta sobre predicciones

### 6.2 Ejemplos de preguntas

| Pregunta | Predice |
|----------|---------|
| "¿Cuánto scrap tendremos mañana?" | Scrap predicho |
| "¿Cuál será la producción del próximo turno?" | Producción forecast |
| "¿Cuándo fallará la prensa X?" | Predicción de fallas |

### 6.3 Interpretar resultados

Cada predicción incluye:

| Elemento | Significado |
|----------|-------------|
| **Valor predicho** | Número estimado |
| **Confianza** | Qué tan seguro está el modelo |
| **Tendencia** | ↑ Arriba, ↓ Abajo, → Estable |
| **Horizonte** | Cuánto tiempo en el futuro |

```
┌────────────────────────────────────────────┐
│ Scrap Predicho - Mañana                    │
│ ═══════════════════════════════════════════   │
│                                             │
│     156 unidades                            │
│     ████████████████████░░░ 78% confianza  │
│                                             │
│     Tendencia: ↑ +12% vs hoy                │
│     Horizonte: 24 horas                     │
└────────────────────────────────────────────┘
```

---

## 7. Historial

### 7.1 Ver historial

El sidebar izquierdo muestra tu historial de consultas:

```
┌──────────────────────────┐
│ HISTORIAL               │
├──────────────────────────┤
│ ● Top scrap hoy         │
│   hace 5 min  ✓         │
│                          │
│ ○ Producción ayer        │
│   hace 1 hora  ✓        │
│                          │
│ ○ Downtime fallas       │
│   hace 2 horas  ⚠       │
└──────────────────────────┘
```

### 7.2 Estados de consultas

| Estado | Significado | Color |
|--------|-------------|-------|
| ✓ Completada | Consulta exitosa | Verde |
| ⚠ Revisión | Requiere validación | Amarillo |
| ✗ Error | Falló la consulta | Rojo |
| ⏳ Pendiente | En proceso | Amarillo |

### 7.3 Acceder a consultas anteriores

1. Haz clic en cualquier consulta del historial
2. Se cargará en el área de resultados
3. Podrás ver el SQL generado y los datos

---

## 8. Gráficos y exportación

### 8.1 Tipos de gráfico

Cuando hay datos temporales, aparecerán botones para cambiar vistas:

```
[Tabla] [Barras] [Líneas]
```

| Tipo | Mejor para |
|------|------------|
| **Tabla** | Datos precisos, copiar |
| **Barras** | Comparaciones |
| **Líneas** | Tendencias |

### 8.2 Exportar datos

Después de una consulta exitosa, verás opciones:

**Para SQL:**
- Copiar como CSV
- Copiar SQL
- Descargar XLSX

**Para PDFs:**
- Ver fuente original

### 8.3 Mostrar/ocultar SQL

```
[Mostrar SQL]  ← Botón para ver el SQL generado
```

Esto es útil para:
- Verificar qué consulta se ejecutó
- Copiar el SQL para usar en otra herramienta
- Depurar problemas

---

## 9. Feedback

### 9.1 Calificar resultados

Después de cada consulta, puedes calificar:

```
┌─────────────────────────────────────────────┐
│ ¿La respuesta fue útil?                     │
│                                             │
│    [👍 Útil]    [👎 No útil]                │
│                                             │
│ Comentario (opcional): ________________     │
└─────────────────────────────────────────────┘
```

### 9.2 Por qué dar feedback

Tu feedback ayuda a:
- **Mejorar respuestas futuras**
- **Entrenar el sistema**
- **Corregir errores**

**Nota:** Si la consulta fue incorrecta, usa el **Panel de Admin** para corregirla.

---

## 10. Solución de problemas

### 10.1 Problemas comunes

| Problema | Solución |
|----------|----------|
| "No encuentro datos" | Verifica que haya datos en el período consultado |
| "Respuesta incorrecta" | Usa el Admin para corregir el SQL |
| "Tarda mucho" | Las primeras consultas son más lentas |
| "Desconectado" | Recarga la página |
| "Error de servidor" | Contacta a tu administrador |

### 10.2 Optimizar consultas

| Consejo | Ejemplo |
|---------|---------|
| Sé específico | "Scrap de la prensa P-001" vs "Scrap" |
| Usa rangos | "Esta semana" vs "Hace mucho" |
| Nombra entidades | "Prensa A" vs "esa máquina" |

### 10.3 Contacto

Para soporte técnico, contacta a tu administrador del sistema.

---

## Atajos de teclado

| Atajo | Acción |
|-------|--------|
| `Enter` | Enviar consulta |
| `Esc` | Cancelar consulta |
| `Ctrl+K` | Limpiar input |
| `Ctrl+H` | Toggle historial |

---

## Glosario

| Término | Significado |
|---------|-------------|
| **Scrap** | Piezas defectuosas |
| **Downtime** | Tiempo de parada de máquina |
| **Prensa** | Máquina de manufactura |
| **Molde** | Herramienta de producción |
| **Turno** | Horario de trabajo |

---

*Manual de Usuario - VannaLight v1.0*
