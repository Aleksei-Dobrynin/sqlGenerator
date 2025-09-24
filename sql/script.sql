-- Создание таблицы для сохраняемых фильтров сотрудников
CREATE TABLE employee_saved_filters (
    id SERIAL PRIMARY KEY,
    employee_id INTEGER NOT NULL,
    filter_name VARCHAR(255) NOT NULL,
    is_default BOOLEAN DEFAULT FALSE,
    is_active BOOLEAN DEFAULT TRUE,
    
    -- Основные параметры пагинации и сортировки
    page_size INTEGER DEFAULT 100,
    page_number INTEGER DEFAULT 0,
    sort_by VARCHAR(100),
    sort_type VARCHAR(10) CHECK (sort_type IN ('asc', 'desc', NULL)),
    
    -- Текстовые фильтры
    pin VARCHAR(50),
    customer_name VARCHAR(255),
    common_filter TEXT,
    address TEXT,
    number VARCHAR(100),
    incoming_numbers TEXT,
    outgoing_numbers TEXT,
    
    -- Даты
    date_start TIMESTAMP,
    date_end TIMESTAMP,
    dashboard_date_start TIMESTAMP,
    dashboard_date_end TIMESTAMP,
    
    -- Массивы ID (используем JSONB для гибкости)
    service_ids JSONB DEFAULT '[]'::JSONB,
    status_ids JSONB DEFAULT '[]'::JSONB,
    structure_ids JSONB DEFAULT '[]'::JSONB,
    app_ids JSONB DEFAULT '[]'::JSONB,
    
    -- Единичные ID ссылки
    district_id INTEGER,
    tag_id INTEGER,
    filter_employee_id INTEGER, -- переименован чтобы не путать с employee_id владельца
    journals_id INTEGER,
    employee_arch_id INTEGER,
    issued_employee_id INTEGER,
    
    -- Tunduk адресация
    tunduk_district_id INTEGER,
    tunduk_address_unit_id INTEGER,
    tunduk_street_id INTEGER,
    
    -- Числовые фильтры
    deadline_day INTEGER DEFAULT 0,
    total_sum_from DECIMAL(15,2),
    total_sum_to DECIMAL(15,2),
    total_payed_from DECIMAL(15,2),
    total_payed_to DECIMAL(15,2),
    
    -- Булевы флаги
    is_expired BOOLEAN DEFAULT FALSE,
    is_my_org_application BOOLEAN DEFAULT FALSE,
    without_assigned_employee BOOLEAN DEFAULT FALSE,
    use_common BOOLEAN DEFAULT TRUE,
    only_count BOOLEAN DEFAULT FALSE,
    is_journal BOOLEAN DEFAULT FALSE,
    is_paid BOOLEAN, -- может быть NULL, TRUE или FALSE
    
    -- Метаданные
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_used_at TIMESTAMP,
    usage_count INTEGER DEFAULT 0,
    
    -- Foreign key на таблицу сотрудников
    CONSTRAINT fk_employee 
        FOREIGN KEY (employee_id) 
        REFERENCES employee(id) 
        ON DELETE CASCADE
);


-- Комментарии к таблице и полям
COMMENT ON TABLE employee_saved_filters IS 'Сохраненные настройки фильтров заявок для сотрудников';
COMMENT ON COLUMN employee_saved_filters.employee_id IS 'ID сотрудника-владельца фильтра';
COMMENT ON COLUMN employee_saved_filters.filter_name IS 'Название сохраненного фильтра';
COMMENT ON COLUMN employee_saved_filters.is_default IS 'Флаг фильтра по умолчанию';
COMMENT ON COLUMN employee_saved_filters.is_active IS 'Флаг активности фильтра';
COMMENT ON COLUMN employee_saved_filters.filter_employee_id IS 'ID сотрудника для фильтрации заявок';
COMMENT ON COLUMN employee_saved_filters.is_paid IS 'Фильтр по оплате: NULL - все, TRUE - оплаченные, FALSE - неоплаченные';
COMMENT ON COLUMN employee_saved_filters.usage_count IS 'Счетчик использования фильтра';
COMMENT ON COLUMN employee_saved_filters.last_used_at IS 'Дата последнего использования';