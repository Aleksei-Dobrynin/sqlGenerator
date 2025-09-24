-- �������� ������� ��� ����������� �������� �����������
CREATE TABLE employee_saved_filters (
    id SERIAL PRIMARY KEY,
    employee_id INTEGER NOT NULL,
    filter_name VARCHAR(255) NOT NULL,
    is_default BOOLEAN DEFAULT FALSE,
    is_active BOOLEAN DEFAULT TRUE,
    
    -- �������� ��������� ��������� � ����������
    page_size INTEGER DEFAULT 100,
    page_number INTEGER DEFAULT 0,
    sort_by VARCHAR(100),
    sort_type VARCHAR(10) CHECK (sort_type IN ('asc', 'desc', NULL)),
    
    -- ��������� �������
    pin VARCHAR(50),
    customer_name VARCHAR(255),
    common_filter TEXT,
    address TEXT,
    number VARCHAR(100),
    incoming_numbers TEXT,
    outgoing_numbers TEXT,
    
    -- ����
    date_start TIMESTAMP,
    date_end TIMESTAMP,
    dashboard_date_start TIMESTAMP,
    dashboard_date_end TIMESTAMP,
    
    -- ������� ID (���������� JSONB ��� ��������)
    service_ids JSONB DEFAULT '[]'::JSONB,
    status_ids JSONB DEFAULT '[]'::JSONB,
    structure_ids JSONB DEFAULT '[]'::JSONB,
    app_ids JSONB DEFAULT '[]'::JSONB,
    
    -- ��������� ID ������
    district_id INTEGER,
    tag_id INTEGER,
    filter_employee_id INTEGER, -- ������������ ����� �� ������ � employee_id ���������
    journals_id INTEGER,
    employee_arch_id INTEGER,
    issued_employee_id INTEGER,
    
    -- Tunduk ���������
    tunduk_district_id INTEGER,
    tunduk_address_unit_id INTEGER,
    tunduk_street_id INTEGER,
    
    -- �������� �������
    deadline_day INTEGER DEFAULT 0,
    total_sum_from DECIMAL(15,2),
    total_sum_to DECIMAL(15,2),
    total_payed_from DECIMAL(15,2),
    total_payed_to DECIMAL(15,2),
    
    -- ������ �����
    is_expired BOOLEAN DEFAULT FALSE,
    is_my_org_application BOOLEAN DEFAULT FALSE,
    without_assigned_employee BOOLEAN DEFAULT FALSE,
    use_common BOOLEAN DEFAULT TRUE,
    only_count BOOLEAN DEFAULT FALSE,
    is_journal BOOLEAN DEFAULT FALSE,
    is_paid BOOLEAN, -- ����� ���� NULL, TRUE ��� FALSE
    
    -- ����������
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_used_at TIMESTAMP,
    usage_count INTEGER DEFAULT 0,
    
    -- Foreign key �� ������� �����������
    CONSTRAINT fk_employee 
        FOREIGN KEY (employee_id) 
        REFERENCES employee(id) 
        ON DELETE CASCADE
);


-- ����������� � ������� � �����
COMMENT ON TABLE employee_saved_filters IS '����������� ��������� �������� ������ ��� �����������';
COMMENT ON COLUMN employee_saved_filters.employee_id IS 'ID ����������-��������� �������';
COMMENT ON COLUMN employee_saved_filters.filter_name IS '�������� ������������ �������';
COMMENT ON COLUMN employee_saved_filters.is_default IS '���� ������� �� ���������';
COMMENT ON COLUMN employee_saved_filters.is_active IS '���� ���������� �������';
COMMENT ON COLUMN employee_saved_filters.filter_employee_id IS 'ID ���������� ��� ���������� ������';
COMMENT ON COLUMN employee_saved_filters.is_paid IS '������ �� ������: NULL - ���, TRUE - ����������, FALSE - ������������';
COMMENT ON COLUMN employee_saved_filters.usage_count IS '������� ������������� �������';
COMMENT ON COLUMN employee_saved_filters.last_used_at IS '���� ���������� �������������';