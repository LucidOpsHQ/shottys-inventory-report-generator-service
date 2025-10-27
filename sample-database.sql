-- Sample PostgreSQL Database Setup for Inventory Report Generator

-- Create database (run this as postgres superuser)
-- CREATE DATABASE inventory_db;

-- Connect to inventory_db and run the following:

-- Create inventory table
CREATE TABLE IF NOT EXISTS inventory (
    id SERIAL PRIMARY KEY,
    product_code VARCHAR(50) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    category VARCHAR(100),
    quantity INTEGER NOT NULL DEFAULT 0,
    unit_price DECIMAL(10, 2) NOT NULL,
    total_value DECIMAL(12, 2) GENERATED ALWAYS AS (quantity * unit_price) STORED,
    supplier VARCHAR(255),
    warehouse_location VARCHAR(100),
    status VARCHAR(50) DEFAULT 'in_stock',
    active BOOLEAN DEFAULT true,
    last_updated TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create index for faster queries
CREATE INDEX IF NOT EXISTS idx_inventory_status ON inventory(status);
CREATE INDEX IF NOT EXISTS idx_inventory_category ON inventory(category);
CREATE INDEX IF NOT EXISTS idx_inventory_active ON inventory(active);

-- Insert sample data
INSERT INTO inventory (product_code, name, category, quantity, unit_price, supplier, warehouse_location, status, active) VALUES
    ('PRD001', 'Laptop Computer', 'Electronics', 45, 899.99, 'Tech Supplier Inc', 'Warehouse A', 'in_stock', true),
    ('PRD002', 'Wireless Mouse', 'Electronics', 150, 29.99, 'Tech Supplier Inc', 'Warehouse A', 'in_stock', true),
    ('PRD003', 'USB-C Cable', 'Accessories', 200, 12.99, 'Cable Co', 'Warehouse B', 'in_stock', true),
    ('PRD004', 'Monitor 27"', 'Electronics', 30, 349.99, 'Display Direct', 'Warehouse A', 'in_stock', true),
    ('PRD005', 'Keyboard Mechanical', 'Electronics', 0, 129.99, 'Tech Supplier Inc', 'Warehouse A', 'out_of_stock', true),
    ('PRD006', 'Desk Lamp', 'Furniture', 75, 45.50, 'Office Plus', 'Warehouse C', 'in_stock', true),
    ('PRD007', 'Office Chair', 'Furniture', 20, 299.99, 'Office Plus', 'Warehouse C', 'in_stock', true),
    ('PRD008', 'Notebook A4', 'Stationery', 500, 3.99, 'Paper World', 'Warehouse B', 'in_stock', true),
    ('PRD009', 'Pen Set (10pcs)', 'Stationery', 300, 8.99, 'Paper World', 'Warehouse B', 'in_stock', true),
    ('PRD010', 'External SSD 1TB', 'Electronics', 15, 179.99, 'Storage Solutions', 'Warehouse A', 'low_stock', true),
    ('PRD011', 'Webcam HD', 'Electronics', 60, 89.99, 'Tech Supplier Inc', 'Warehouse A', 'in_stock', true),
    ('PRD012', 'Desk Organizer', 'Accessories', 100, 19.99, 'Office Plus', 'Warehouse C', 'in_stock', true),
    ('PRD013', 'Whiteboard Markers', 'Stationery', 250, 5.99, 'Paper World', 'Warehouse B', 'in_stock', true),
    ('PRD014', 'HDMI Cable 2m', 'Accessories', 120, 15.99, 'Cable Co', 'Warehouse B', 'in_stock', true),
    ('PRD015', 'Printer Paper A4', 'Stationery', 400, 6.99, 'Paper World', 'Warehouse B', 'in_stock', true);

-- Create a view for low stock items
CREATE OR REPLACE VIEW low_stock_items AS
SELECT
    id,
    product_code,
    name,
    category,
    quantity,
    unit_price,
    total_value,
    supplier,
    warehouse_location
FROM inventory
WHERE quantity < 25 AND active = true
ORDER BY quantity ASC;

-- Create a view for inventory summary by category
CREATE OR REPLACE VIEW inventory_summary_by_category AS
SELECT
    category,
    COUNT(*) as product_count,
    SUM(quantity) as total_quantity,
    SUM(total_value) as total_value,
    AVG(unit_price) as avg_unit_price
FROM inventory
WHERE active = true
GROUP BY category
ORDER BY total_value DESC;

-- Sample queries you can use with the API:

-- Get all active inventory
-- SELECT * FROM inventory WHERE active = true ORDER BY name;

-- Get low stock items
-- SELECT * FROM low_stock_items;

-- Get inventory by category
-- SELECT * FROM inventory WHERE category = 'Electronics' ORDER BY name;

-- Get inventory summary
-- SELECT * FROM inventory_summary_by_category;

-- Get high value items
-- SELECT * FROM inventory WHERE total_value > 1000 ORDER BY total_value DESC;

-- Get items by warehouse
-- SELECT * FROM inventory WHERE warehouse_location = 'Warehouse A' ORDER BY name;
